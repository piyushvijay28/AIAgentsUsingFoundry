using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

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
    private readonly ShopAxisDbContext _db;
    private readonly OrderStatusTool _orderTool;

    public ReturnInitiationTool(ShopAxisDbContext db, OrderStatusTool orderTool)
    {
        _db        = db;
        _orderTool = orderTool;
    }

    public ToolResult Execute(
        string orderId, string customerEmail,
        string reasonCode, string[] itemSkus)
    {
        var order = _orderTool.GetById(orderId);

        if (order == null)
            return ToolResult.Fail("order_not_found");

        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        if (order.DeliveryDate == null)
            return ToolResult.Fail("order_not_yet_delivered");

        var today      = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysSince  = today.DayNumber - order.DeliveryDate.Value.DayNumber;

        if (daysSince > ReturnWindowDays)
            return ToolResult.Fail(
                $"outside_return_window:delivered={order.DeliveryDate.Value:yyyy-MM-dd}," +
                $"days_since={daysSince},window={ReturnWindowDays}");

        var orderSkus   = order.Items.Select(i => i.Sku).ToHashSet();
        var invalidSkus = itemSkus.Where(s => !orderSkus.Contains(s)).ToList();

        if (invalidSkus.Count > 0)
            return ToolResult.Fail($"skus_not_on_order:{string.Join(",", invalidSkus)}");

        var refundAmount = order.Items
            .Where(i => itemSkus.Contains(i.Sku))
            .Sum(i => i.Price * i.Qty);

        var rmaNumber          = "RMA-" + Guid.NewGuid().ToString("N").ToUpper()[..8];
        var expectedCompletion = today.AddDays(5);

        // ── Save to database ──────────────────────────────────────────────
        var record = new ReturnRecord
        {
            RmaNumber          = rmaNumber,
            OrderId            = orderId,
            ReasonCode         = reasonCode,
            RefundStage        = "return_requested",
            RefundAmount       = refundAmount,
            PaymentMethod      = "original_payment_method",
            LabelUrl           = $"https://returns.shopaxis.com/label/{rmaNumber}",
            ExpectedCompletion = expectedCompletion,
            CreatedUtc         = DateTime.UtcNow,
            ReturnItems        = itemSkus.Select(s => new ReturnItem { Sku = s }).ToList()
        };

        _db.Returns.Add(record);
        _db.SaveChanges();

        return ToolResult.Ok(new
        {
            rma_number           = rmaNumber,
            return_label_url     = record.LabelUrl,
            refund_amount        = refundAmount,
            currency             = "GBP",
            expected_refund_date = expectedCompletion.ToString("yyyy-MM-dd"),
            items_accepted       = itemSkus,
            next_steps           =
                "Print your return label and drop off at any authorised carrier location within 7 days."
        });
    }

    public ReturnRecord? GetByRma(string rmaNumber) =>
        _db.Returns
           .Include(r => r.ReturnItems)
           .FirstOrDefault(r => r.RmaNumber == rmaNumber);
}