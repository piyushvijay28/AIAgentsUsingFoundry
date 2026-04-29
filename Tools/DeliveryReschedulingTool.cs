public class DeliveryReschedulingTool
{
    public static readonly string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "order_id":          { "type": "string", "pattern": "^ORD-[0-9]{8}$" },
        "customer_email":    { "type": "string", "format": "email" },
        "new_delivery_date": { "type": "string", "format": "date" },
        "delivery_window": {
          "type": "string",
          "enum": ["morning","afternoon","evening","all_day"]
        }
      },
      "required": ["order_id", "customer_email", "new_delivery_date"],
      "additionalProperties": false
    }
    """;

    private static readonly string[] ReschedulableStatuses =
        ["processing", "shipped", "out_for_delivery"];

    private static bool CheckCarrierAvailability(DateOnly date) =>
        date.DayOfWeek != DayOfWeek.Sunday;

    private readonly ShopAxisDbContext _db;
    private readonly OrderStatusTool   _orderTool;

    public DeliveryReschedulingTool(ShopAxisDbContext db, OrderStatusTool orderTool)
    {
        _db        = db;
        _orderTool = orderTool;
    }

    public ToolResult Execute(
        string orderId, string customerEmail,
        string newDeliveryDate, string? deliveryWindow)
    {
        var order = _orderTool.GetById(orderId);

        if (order == null)
            return ToolResult.Fail("order_not_found");

        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        if (!ReschedulableStatuses.Contains(order.Status))
            return ToolResult.Fail($"cannot_reschedule_status:{order.Status}");

        if (!DateOnly.TryParse(newDeliveryDate, out var requestedDate))
            return ToolResult.Fail("invalid_date_format");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (requestedDate < today)
            return ToolResult.Fail(
                $"date_must_be_future:requested={requestedDate},today={today}");

        if (requestedDate <= order.EstimatedDelivery)
            return ToolResult.Fail(
                $"date_before_or_equal_to_original_delivery:" +
                $"original_eta={order.EstimatedDelivery:yyyy-MM-dd}," +
                $"requested={requestedDate:yyyy-MM-dd}");

        if (requestedDate.DayNumber - today.DayNumber > 14)
            return ToolResult.Fail(
                $"date_exceeds_14_day_window:max={today.AddDays(14):yyyy-MM-dd}");

        if (!CheckCarrierAvailability(requestedDate))
            return ToolResult.Fail(
                $"carrier_unavailable_on_sunday:suggested={requestedDate.AddDays(1):yyyy-MM-dd}");

        var originalDate = order.EstimatedDelivery;

        // ── Save to database ──────────────────────────────────────────────
        order.EstimatedDelivery = requestedDate;
        _db.Orders.Update(order);
        _db.SaveChanges();

        var confirmationId = ("RSCH-" + Guid.NewGuid().ToString("N").ToUpper())[..12];

        return ToolResult.Ok(new
        {
            confirmation_id   = confirmationId,
            order_id          = orderId,
            carrier           = order.Carrier,
            original_date     = originalDate.ToString("yyyy-MM-dd"),
            new_delivery_date = requestedDate.ToString("yyyy-MM-dd"),
            delivery_window   = deliveryWindow ?? "all_day",
            message           =
                $"Your delivery has been postponed from " +
                $"{originalDate:dddd, MMMM d, yyyy} to " +
                $"{requestedDate:dddd, MMMM d, yyyy} ({deliveryWindow ?? "all day"})."
        });
    }
}