using System.Text.RegularExpressions;

public class RefundStatusTool
{
    // ── JSON Schema ──────────────────────────────────────────────────────────
    public static readonly string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "return_id": {
          "type": "string",
          "pattern": "^RMA-[A-Z0-9]{8}$",
          "description": "RMA number from the return initiation step"
        },
        "customer_email": {
          "type": "string",
          "format": "email"
        }
      },
      "required": ["return_id", "customer_email"],
      "additionalProperties": false
    }
    """;

    // Simulated refund pipeline stages in order
    private static readonly string[] RefundStages =
    [
        "return_requested",
        "item_in_transit",
        "item_received",
        "quality_check",
        "refund_approved",
        "refund_processing",
        "refund_issued"
    ];

    private readonly ReturnInitiationTool _returnTool;
    private readonly OrderStatusTool _orderTool;

    public RefundStatusTool(ReturnInitiationTool returnTool, OrderStatusTool orderTool)
    {
        _returnTool = returnTool;
        _orderTool  = orderTool;
    }

    // ── Execute ──────────────────────────────────────────────────────────────
    public ToolResult Execute(string returnId, string customerEmail)
    {
        if (!Regex.IsMatch(returnId, @"^RMA-[A-Z0-9]{8}$"))
            return ToolResult.Fail("invalid_rma_format");

        var returnRecord = _returnTool.GetByRma(returnId);

        if (returnRecord == null)
            return ToolResult.Fail("return_not_found");

        // Ownership check via the linked order
        var order = _orderTool.GetById(returnRecord.OrderId);

        if (order == null)
            return ToolResult.Fail("linked_order_not_found");

        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        // Simulate stage progression based on time since creation
        var hoursSinceCreated = (DateTime.UtcNow - returnRecord.CreatedUtc).TotalHours;
        var stageIndex = Math.Min((int)(hoursSinceCreated / 12), RefundStages.Length - 1);
        var currentStage = RefundStages[stageIndex];

        return ToolResult.Ok(new
        {
            return_id           = returnId,
            order_id            = returnRecord.OrderId,
            refund_stage        = currentStage,
            refund_amount       = returnRecord.RefundAmount,
            currency            = "GBP",
            expected_completion = returnRecord.ExpectedCompletion.ToString("yyyy-MM-dd"),
            payment_method      = returnRecord.PaymentMethod,
            items_returning     = returnRecord.ItemSkus,
            stage_description   = GetStageDescription(currentStage)
        });
    }

    private static string GetStageDescription(string stage) => stage switch
    {
        "return_requested"  => "Your return has been registered. Please ship the item using your label.",
        "item_in_transit"   => "We have received your shipment tracking update.",
        "item_received"     => "Your item has arrived at our returns centre.",
        "quality_check"     => "Our team is inspecting the returned item.",
        "refund_approved"   => "Your refund has been approved.",
        "refund_processing" => "Your refund is being processed by our payments team.",
        "refund_issued"     => "Your refund has been issued to your original payment method.",
        _                   => "Status unknown."
    };
}