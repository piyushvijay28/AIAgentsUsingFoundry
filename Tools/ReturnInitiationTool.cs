using System.Text.RegularExpressions;

public class ReturnInitiationTool
{
    public static readonly string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "order_id":       { "type": "string", "pattern": "^ORD-[0-9]{8}$" },
        "customer_email": { "type": "string", "format": "email" },
        "reason_code": {
          "type": "string",
          "enum": ["defective","wrong_item","not_as_described","changed_mind","damaged_in_transit"]
        },
        "item_skus": {
          "type": "array",
          "items": { "type": "string", "pattern": "^SKU-[0-9]{4}$" },
          "minItems": 1,
          "maxItems": 20
        }
      },
      "required": ["order_id", "customer_email", "reason_code", "item_skus"],
      "additionalProperties": false
    }
    """;

    private const int ReturnWindowDays = 30;
    private static readonly List<ReturnRecord> _returnsDb = [];
    private readonly OrderStatusTool _orderTool;

    public ReturnInitiationTool(OrderStatusTool orderTool)
    {
        _orderTool = orderTool;
    }

    public ToolResult Execute(
        string orderId,
        string customerEmail,
        string reasonCode,
        string[] itemSkus)
    {
        var order = _orderTool.GetById(orderId);

        if (order == null)
            return ToolResult.Fail("order_not_found");

        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        if (order.DeliveryDate == null)
            return ToolResult.Fail("order_not_yet_delivered");

        // ✅ FIX: Use .DayNumber to subtract DateOnly values
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysSince = today.DayNumber - order.DeliveryDate.Value.DayNumber;

        if (daysSince > ReturnWindowDays)
            return ToolResult.Fail($"outside_return_window:{daysSince}_days_since_delivery");

        var orderSkus = order.Items.Select(i => i.Sku).ToHashSet();
        var invalidSkus = itemSkus.Where(s => !orderSkus.Contains(s)).ToList();

        if (invalidSkus.Count > 0)
            return ToolResult.Fail($"skus_not_on_order:{string.Join(",", invalidSkus)}");

        var refundAmount = order.Items
            .Where(i => itemSkus.Contains(i.Sku))
            .Sum(i => i.Price * i.Qty);

        var rmaNumber = ("RMA-" + Guid.NewGuid().ToString("N").ToUpper())[..12];
        var expectedCompletion = today.AddDays(5);

        var record = new ReturnRecord
        {
            RmaNumber          = rmaNumber,
            OrderId            = orderId,
            ReasonCode         = reasonCode,
            ItemSkus           = itemSkus,
            RefundStage        = "return_requested",
            RefundAmount       = refundAmount,
            PaymentMethod      = "original_payment_method",
            LabelUrl           = $"https://returns.shopaxis.com/label/{rmaNumber}",
            ExpectedCompletion = expectedCompletion,
            CreatedUtc         = DateTime.UtcNow
        };

        _returnsDb.Add(record);

        return ToolResult.Ok(new
        {
            rma_number           = rmaNumber,
            return_label_url     = record.LabelUrl,
            refund_amount        = refundAmount,
            currency             = "GBP",
            expected_refund_date = expectedCompletion.ToString("yyyy-MM-dd"),
            items_accepted       = itemSkus
        });
    }

    public ReturnRecord? GetByRma(string rmaNumber) =>
        _returnsDb.FirstOrDefault(r => r.RmaNumber == rmaNumber);
}