using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using System.Diagnostics;
using System.Text.Json;

// ── 1. Wire up tools ──────────────────────────────────────────────────────
var orderTool = new OrderStatusTool();
var returnTool = new ReturnInitiationTool(orderTool);
var rescheduleTool = new DeliveryReschedulingTool(orderTool);
var refundTool = new RefundStatusTool(returnTool, orderTool);
var safetyEval = new ContentSafetyEvaluator();

// ── 2. Client setup ───────────────────────────────────────────────────────
var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException(
        "Missing FOUNDRY_PROJECT_ENDPOINT.\n" +
        "Set it to: https://<resource>.services.ai.azure.com/api/projects/<project>");

var modelName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME") ?? "gpt-4o";

TokenCredential credential =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
        ? new AzureCliCredential()
        : new DefaultAzureCredential();

AIProjectClient projectClient = new(new Uri(endpoint), credential);
PersistentAgentsClient agentsClient = projectClient.GetPersistentAgentsClient();

// ── 3. Tool definitions ───────────────────────────────────────────────────
var toolDefinitions = new List<ToolDefinition>
{
    new FunctionToolDefinition(
        name: "get_order_status",
        description: "Retrieve order status, carrier and ETA after identity verified.",
        parameters: BinaryData.FromString(OrderStatusTool.JsonSchema)),

    new FunctionToolDefinition(
        name: "initiate_return",
        description: "Initiate a product return within the 30-day delivery window.",
        parameters: BinaryData.FromString(ReturnInitiationTool.JsonSchema)),

    new FunctionToolDefinition(
        name: "reschedule_delivery",
        description: "Reschedule a pending shipment to a new future date.",
        parameters: BinaryData.FromString(DeliveryReschedulingTool.JsonSchema)),

    new FunctionToolDefinition(
        name: "get_refund_status",
        description: "Query refund pipeline stage and expected completion for a return.",
        parameters: BinaryData.FromString(RefundStatusTool.JsonSchema)),
};

// ── 4. Create agent ───────────────────────────────────────────────────────
PersistentAgent agent = agentsClient.Administration.CreateAgent(
    model: modelName,
    name: "ShopAxis-Customer-Operations-Agent",
    instructions: SystemInstructions.Full,
    tools: toolDefinitions);

Console.WriteLine($"Agent created: {agent.Id}\n");

// ── 5. Tool dispatcher ────────────────────────────────────────────────────
string DispatchTool(string name, string argsJson)
{
    var args = JsonDocument.Parse(argsJson).RootElement;

    ToolResult result = name switch
    {
        "get_order_status" => orderTool.Execute(
            args.GetProperty("order_id").GetString()!,
            args.GetProperty("customer_email").GetString()!),

        "initiate_return" => returnTool.Execute(
            args.GetProperty("order_id").GetString()!,
            args.GetProperty("customer_email").GetString()!,
            args.GetProperty("reason_code").GetString()!,
            args.GetProperty("item_skus")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray()),

        "reschedule_delivery" => rescheduleTool.Execute(
            args.GetProperty("order_id").GetString()!,
            args.GetProperty("customer_email").GetString()!,
            args.GetProperty("new_delivery_date").GetString()!,
            args.TryGetProperty("delivery_window", out var dw) ? dw.GetString() : null),

        "get_refund_status" => refundTool.Execute(
            args.GetProperty("return_id").GetString()!,
            args.GetProperty("customer_email").GetString()!),

        _ => ToolResult.Fail("unknown_tool")
    };

    return result.ToJson();
}

// ── 6. Conversation runner ────────────────────────────────────────────────
async Task<string> RunAsync(string userMessage, string sessionId)
{
    var safety = await safetyEval.EvaluateAsync(userMessage);
    if (safety.ShouldSuspend)
    {
        AuditLogger.LogSafetyEvent(sessionId, userMessage, safety);
        var (modded, _) = ToneModulator.Modulate(userMessage, safety);
        userMessage = modded;
    }

    PersistentAgentThread thread = agentsClient.Threads.CreateThread();

    try
    {
        agentsClient.Messages.CreateMessage(
            threadId: thread.Id,
            role: MessageRole.User,
            content: userMessage);

        ThreadRun run = agentsClient.Runs.CreateRun(
            threadId: thread.Id,
            assistantId: agent.Id);

        do
        {
            await Task.Delay(1500);
            run = agentsClient.Runs.GetRun(thread.Id, run.Id);

            if (run.Status == RunStatus.RequiresAction &&
                run.RequiredAction is SubmitToolOutputsAction submitAction)
            {
                var toolOutputs = new List<ToolOutput>();

                foreach (RequiredToolCall call in submitAction.ToolCalls)
                {
                    if (call is not RequiredFunctionToolCall funcCall)
                        continue;

                    var sw = Stopwatch.StartNew();
                    string result = DispatchTool(funcCall.Name, funcCall.Arguments);
                    sw.Stop();

                    toolOutputs.Add(new ToolOutput(funcCall.Id, result));

                    AuditLogger.LogToolCall(new AuditEntry
                    {
                        SessionId = sessionId,
                        ThreadId = thread.Id,
                        RunId = run.Id,
                        ToolName = funcCall.Name,
                        Arguments = funcCall.Arguments,
                        Result = result,
                        LatencyMs = (int)sw.ElapsedMilliseconds,
                        TimestampUtc = DateTime.UtcNow
                    });
                }

                run = agentsClient.Runs.SubmitToolOutputsToRun(run, toolOutputs);
            }

        } while (run.Status == RunStatus.Queued ||
                 run.Status == RunStatus.InProgress ||
                 run.Status == RunStatus.RequiresAction);

        if (run.Status == RunStatus.Failed)
        {
            return $"Run failed: {run.LastError?.Code} — {run.LastError?.Message}";
        }

        var responseParts = new List<string>();

        Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(
            threadId: thread.Id,
            order: ListSortOrder.Ascending);

        foreach (PersistentThreadMessage msg in messages)
        {
            if (msg.Role != MessageRole.Agent) continue;

            foreach (MessageContent item in msg.ContentItems)
            {
                if (item is MessageTextContent textItem)
                    responseParts.Add(textItem.Text);
            }
        }

        return string.Join("\n", responseParts);
    }
    finally
    {
        agentsClient.Threads.DeleteThread(thread.Id);
    }
}

// ── 7. Interactive console chat ───────────────────────────────────────────
Console.WriteLine("ShopAxis Support Agent");
Console.WriteLine("Type 'exit' to quit.\n");

string sessionId = Guid.NewGuid().ToString();
Console.WriteLine($"Session ID: {sessionId}");

while (true)
{
    Console.Write("\nYOU: ");
    string? userMessage = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userMessage))
        continue;

    if (userMessage.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    try
    {
        string response = await RunAsync(userMessage, sessionId);
        Console.WriteLine($"\nAGENT: {response}\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nERROR: {ex.Message}\n");
    }
}

// Optional cleanup
agentsClient.Administration.DeleteAgent(agent.Id);
Console.WriteLine("\nGoodbye!");
