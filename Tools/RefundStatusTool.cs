using System.Text.RegularExpressions;

public class RefundStatusTool
{
    public static readonly string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "return_id":      { "type": "string", "pattern": "^RMA-[A-Z0-9]{8}$" },
        "customer_email": { "type": "string", "format": "email" }
      },
      "required": ["return_id", "customer_email"],
      "additionalProperties": false
    }
    """;

    private static readonly string[] RefundStages =
    [
        "return_requested", "item_in_transit", "item_received",
        "quality_check", "refund_approved", "refund_processing", "refund_issued"
    ];

    private readonly ReturnInitiationTool _returnTool;
    private readonly OrderStatusTool      _orderTool;

    public RefundStatusTool(ReturnInitiationTool returnTool, OrderStatusTool orderTool)
    {
        _returnTool = returnTool;
        _orderTool  = orderTool;
    }

    public ToolResult Execute(string returnId, string customerEmail)
    {
        if (!Regex.IsMatch(returnId, @"^RMA-[A-Z0-9]{8}$"))
            return ToolResult.Fail("invalid_rma_format");

        var returnRecord = _returnTool.GetByRma(returnId);

        if (returnRecord == null)
            return ToolResult.Fail("return_not_found");

        var order = _orderTool.GetById(returnRecord.OrderId);

        if (order == null)
            return ToolResult.Fail("linked_order_not_found");

        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        var hoursSince  = (DateTime.UtcNow - returnRecord.CreatedUtc).TotalHours;
        var stageIndex  = Math.Min((int)(hoursSince / 12), RefundStages.Length - 1);
        var currentStage = RefundStages[stageIndex];
        var today       = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysRemaining = Math.Max(0,
            returnRecord.ExpectedCompletion.DayNumber - today.DayNumber);

        return ToolResult.Ok(new
        {
            return_id           = returnId,
            order_id            = returnRecord.OrderId,
            refund_stage        = currentStage,
            refund_amount       = returnRecord.RefundAmount,
            currency            = "GBP",
            expected_completion = returnRecord.ExpectedCompletion.ToString("yyyy-MM-dd"),
            days_remaining      = daysRemaining,
            payment_method      = returnRecord.PaymentMethod,
            items_returning     = returnRecord.ItemSkus,
            stage_description   = GetStageDescription(currentStage),
            progress_percent    = (int)((stageIndex + 1) / (double)RefundStages.Length * 100)
        });
    }

    private static string GetStageDescription(string stage) => stage switch
    {
        "return_requested"  => "Return registered. Please ship using the provided label.",
        "item_in_transit"   => "Shipment is on its way back to us.",
        "item_received"     => "Item arrived at our returns centre.",
        "quality_check"     => "Our team is inspecting the returned item.",
        "refund_approved"   => "Refund approved and queued for payment.",
        "refund_processing" => "Refund is being processed — 1-2 business days.",
        "refund_issued"     => "Refund sent to your original payment method.",
        _                   => "Status unknown — please contact support."
    };
}