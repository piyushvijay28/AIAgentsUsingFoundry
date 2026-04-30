# ShopAxis AI Customer Operations Agent

An intelligent transactional AI agent built on **Azure AI Foundry** and **GPT-4o** that automates four core customer service operations for the ShopAxis e-commerce platform. The agent executes real transactions — not just answers questions — by calling custom function tools connected to a live Azure SQL database.

---

## What It Does

| Operation | Description |
|---|---|
| **Order Status Lookup** | Retrieves order status, carrier, tracking number, and estimated delivery |
| **Return Initiation** | Validates 30-day return window and generates an RMA number |
| **Delivery Rescheduling** | Postpones delivery to a new date within carrier availability rules |
| **Refund Status Tracking** | Queries the refund pipeline stage and expected completion date |

---

## Architecture

```
Customer Message
      │
      ▼
Content Safety Evaluator ──── Abusive ──── Transaction Suspended → Human Escalation
      │
   Frustrated
      │
      ▼
[TONE:EMPATHETIC] prefix added → Agent empathises first, then processes
      │
   Clean
      │
      ▼
Azure AI Foundry Agent (GPT-4o)
      │
      ▼
Tool Dispatcher (Program.cs)
      ├── OrderStatusTool
      ├── ReturnInitiationTool
      ├── DeliveryReschedulingTool
      └── RefundStatusTool
              │
              ▼
      Azure SQL Database
      (Orders, Returns, AuditLog)
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 10 |
| AI Agent | Azure AI Foundry + GPT-4o |
| Agent SDK | Azure.AI.Projects 2.0.1 + Azure.AI.Agents.Persistent |
| Content Safety | Azure.AI.ContentSafety 1.0.0 |
| Database ORM | Entity Framework Core — Code First |
| Database | Azure SQL Database |
| Authentication | Azure.Identity — AzureCliCredential / DefaultAzureCredential |

---

## Project Structure

```
ShopAxis.Agent/
├── Program.cs                        # Agent runner, tool dispatcher, chat loop
├── Data/
│   ├── ShopAxisDbContext.cs           # EF Core DbContext with seed data
│   └── ShopAxisDbContextFactory.cs   # Design-time factory for migrations
├── Models/
│   ├── OrderRecord.cs                # Order entity
│   ├── OrderItem.cs                  # Order line item entity
│   ├── ReturnRecord.cs               # Return/RMA entity
│   ├── ReturnItem.cs                 # Return line item entity
│   ├── AuditEntry.cs                 # Audit log entity
│   └── ToolResult.cs                 # Tool response wrapper
├── Tools/
│   ├── OrderStatusTool.cs            # JSON schema + order lookup logic
│   ├── ReturnInitiationTool.cs       # JSON schema + return creation logic
│   ├── DeliveryReschedulingTool.cs   # JSON schema + reschedule logic
│   └── RefundStatusTool.cs           # JSON schema + refund query logic
├── Safety/
│   ├── ContentSafetyEvaluator.cs     # Azure Content Safety API + keyword detection
│   ├── ToneModulator.cs              # Decides: proceed / empathise / suspend
│   └── SafetyResult.cs              # Safety evaluation result model
├── Audit/
│   └── AuditLogger.cs               # Logs every tool call to Azure SQL
└── Instructions/
    └── SystemInstructions.cs         # Full agent system prompt
```

---

## Prerequisites

- .NET 10 SDK
- Azure Subscription
- Azure CLI (`az login` for local development)
- The following Azure resources created:

| Resource | Purpose |
|---|---|
| Azure AI Foundry Project | Hosts the GPT-4o agent and conversation threads |
| GPT-4o Model Deployment | Language model powering the agent |
| Azure AI Content Safety | Pre-screens messages for harmful content |
| Azure SQL Database | Persists orders, returns, and audit log |

---

## Environment Variables

Set all five before running:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT  = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$env:FOUNDRY_MODEL_NAME        = "gpt-4o"
$env:CONTENT_SAFETY_ENDPOINT   = "https://<resource>.cognitiveservices.azure.com/"
$env:CONTENT_SAFETY_KEY        = "<your-key>"
$env:SQL_CONNECTION_STRING     = "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=ShopAxisDb;User ID=<user>;Password=<pass>;Encrypt=True;"
$env:ASPNETCORE_ENVIRONMENT    = "Development"
```

| Variable | Where to Find |
|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | AI Foundry portal → Project → Overview → Project endpoint |
| `FOUNDRY_MODEL_NAME` | AI Foundry portal → Models + Endpoints → Name column |
| `CONTENT_SAFETY_ENDPOINT` | Azure Portal → Content Safety → Keys and Endpoint |
| `CONTENT_SAFETY_KEY` | Azure Portal → Content Safety → Keys and Endpoint → Key 1 |
| `SQL_CONNECTION_STRING` | Azure Portal → SQL Database → Connection strings → ADO.NET |

---

## Setup and Run

```bash
# Restore packages
dotnet restore

# Create database and apply migrations
dotnet ef database update

# Run the agent
dotnet run
```

On startup the agent will:
1. Connect to Azure SQL and apply any pending migrations
2. Provision a GPT-4o agent with all four tools registered
3. Create a persistent conversation thread
4. Start the interactive chat loop

---

## Key Design Decisions

**Identity verification as a hard gate** — The system instructions require name, email, and order ID before any tool can be invoked. This is enforced at the instruction layer first, with email ownership verified again inside each tool as a secondary guard.

**Defence in depth** — Every business rule is enforced twice: once in the system instructions (natural language) and once in the tool code (deterministic C#). This ensures rules hold even if the model misinterprets an instruction.

**Frustrated vs abusive detection** — Content Safety scores are combined with local keyword detection. A frustrated customer (high hate, low violence) gets their transaction processed with an empathetic tone. An abusive customer (high hate and violence, or threatening keywords) has their transaction suspended immediately and is routed to a human agent.

**Postpone-only rescheduling** — The reschedule tool validates that the new delivery date must be strictly after the original estimated delivery date. Rescheduling can only postpone — never bring forward.

**Persistent thread per session** — One thread is created per customer session and reused for all messages. This gives the agent full conversation memory without asking the customer to repeat themselves.

---

## Seeded Test Accounts

| Customer | Email | Order ID | Status | Test Scenario |
|---|---|---|---|---|
| Sarah Chen | sarah.chen@example.com | ORD-00482917 | Shipped | Status lookup, reschedule |
| James Park | james.park@example.com | ORD-00391045 | Delivered 35 days ago | Outside return window |
| Angry Customer | angry@example.com | ORD-00512334 | Out for delivery | Content safety |
| Lisa Wang | lisa.wang@example.com | ORD-00734521 | Delivered 20 days ago | Return initiation, refund |

---

## Audit Log

Every tool invocation is recorded to the `AuditLog` table in Azure SQL:

```json
{
  "timestamp_utc": "2026-04-29T10:23:01Z",
  "session_id":    "a8d0ea39-...",
  "thread_id":     "thread_iGzc...",
  "tool_name":     "initiate_return",
  "result_status": "success",
  "latency_ms":    24
}
```

Query the log in Azure Portal → SQL Database → Query Editor:

```sql
SELECT ToolName, Result, LatencyMs, TimestampUtc
FROM AuditLog
ORDER BY TimestampUtc DESC;
```

---

## Authorisation Rules Summary

| Tool | Key Rules |
|---|---|
| `get_order_status` | Identity verified → email must match order record |
| `initiate_return` | Order must be delivered → within 30 days of delivery date |
| `reschedule_delivery` | Status must be processing/shipped/out_for_delivery → new date after original ETA → within 14 days → not Sunday |
| `get_refund_status` | Valid RMA number required → email must match linked order |
