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

    private readonly OrderStatusTool _orderTool;

    public DeliveryReschedulingTool(OrderStatusTool orderTool)
    {
        _orderTool = orderTool;
    }

    public ToolResult Execute(
        string orderId,
        string customerEmail,
        string newDeliveryDate,
        string? deliveryWindow)
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

        if (requestedDate <= today)
            return ToolResult.Fail("date_must_be_future");

        if (requestedDate.DayNumber - today.DayNumber > 14)
            return ToolResult.Fail("date_exceeds_14_day_window");

        if (!CheckCarrierAvailability(requestedDate))
            return ToolResult.Fail("carrier_unavailable_on_requested_date");

        order.EstimatedDelivery = requestedDate;

        var confirmationId = ("RSCH-" + Guid.NewGuid().ToString("N").ToUpper())[..12];

        return ToolResult.Ok(new
        {
            confirmation_id   = confirmationId,
            order_id          = orderId,
            new_delivery_date = requestedDate.ToString("yyyy-MM-dd"),
            delivery_window   = deliveryWindow ?? "all_day",
            carrier           = order.Carrier,
            message           = $"Your delivery has been rescheduled to {requestedDate:dddd, MMMM d}."
        });
    }
}