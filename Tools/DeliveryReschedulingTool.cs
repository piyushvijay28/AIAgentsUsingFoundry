public class DeliveryReschedulingTool
{
    public static readonly string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "order_id": {
          "type": "string",
          "pattern": "^ORD-[0-9]{8}$"
        },
        "customer_email": {
          "type": "string",
          "format": "email"
        },
        "new_delivery_date": {
          "type": "string",
          "format": "date",
          "description": "New delivery date YYYY-MM-DD. Must be after original estimated delivery date, within 14 days from today, and not a Sunday."
        },
        "delivery_window": {
          "type": "string",
          "enum": ["morning", "afternoon", "evening", "all_day"],
          "description": "Preferred delivery time window"
        }
      },
      "required": ["order_id", "customer_email", "new_delivery_date"],
      "additionalProperties": false
    }
    """;

    private static readonly string[] ReschedulableStatuses =
        ["processing", "shipped", "out_for_delivery"];

    // Simulated carrier availability — unavailable on Sundays
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
        // ── Order lookup ──────────────────────────────────────────────────
        var order = _orderTool.GetById(orderId);

        if (order == null)
            return ToolResult.Fail("order_not_found");

        // ── Ownership check ───────────────────────────────────────────────
        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        // ── Status check — only reschedulable if not yet delivered ────────
        if (!ReschedulableStatuses.Contains(order.Status))
            return ToolResult.Fail(
                $"cannot_reschedule_status:{order.Status}:" +
                $"rescheduling_only_available_for_processing_shipped_or_out_for_delivery_orders");

        // ── Date parsing ──────────────────────────────────────────────────
        if (!DateOnly.TryParse(newDeliveryDate, out var requestedDate))
            return ToolResult.Fail(
                "invalid_date_format:expected_yyyy-MM-dd");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── Rule 1: Must not be in the past ───────────────────────────────
        if (requestedDate < today)
            return ToolResult.Fail(
                $"date_must_be_future:" +
                $"requested={requestedDate:yyyy-MM-dd}," +
                $"today={today:yyyy-MM-dd}");

        // ── Rule 2: Must be AFTER original estimated delivery date ─────────
        // Rescheduling only postpones delivery — cannot bring it forward
        if (requestedDate <= order.EstimatedDelivery)
            return ToolResult.Fail(
                $"date_before_or_equal_to_original_delivery:" +
                $"original_eta={order.EstimatedDelivery:yyyy-MM-dd}," +
                $"requested={requestedDate:yyyy-MM-dd}:" +
                $"new date must be after {order.EstimatedDelivery:yyyy-MM-dd}");

        // ── Rule 3: Must be within 14 days from today ─────────────────────
        if (requestedDate.DayNumber - today.DayNumber > 14)
            return ToolResult.Fail(
                $"date_exceeds_14_day_window:" +
                $"requested={requestedDate:yyyy-MM-dd}," +
                $"max_allowed={today.AddDays(14):yyyy-MM-dd}");

        // ── Rule 4: Carrier unavailable on Sundays ────────────────────────
        if (!CheckCarrierAvailability(requestedDate))
            return ToolResult.Fail(
                $"carrier_unavailable_on_sunday:" +
                $"requested={requestedDate:yyyy-MM-dd}," +
                $"suggested={requestedDate.AddDays(1):yyyy-MM-dd}");

        // ── All checks passed — update the record ─────────────────────────
        var originalDate = order.EstimatedDelivery;
        order.EstimatedDelivery = requestedDate;

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
                $"{requestedDate:dddd, MMMM d, yyyy} " +
                $"({deliveryWindow ?? "all day"})."
        });
    }
}