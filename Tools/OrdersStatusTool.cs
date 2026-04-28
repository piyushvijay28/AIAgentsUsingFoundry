using System.Text.RegularExpressions;

public class OrderStatusTool
{
    // ── JSON Schema ──────────────────────────────────────────────────────────
    public static readonly string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "order_id": {
          "type": "string",
          "pattern": "^ORD-[0-9]{8}$",
          "description": "Order identifier in format ORD-XXXXXXXX"
        },
        "customer_email": {
          "type": "string",
          "format": "email",
          "description": "Customer email address used to verify ownership"
        }
      },
      "required": ["order_id", "customer_email"],
      "additionalProperties": false
    }
    """;

    // ── Simulated back-end data fixture ──────────────────────────────────────
    private readonly Dictionary<string, OrderRecord> _ordersDb = new()
    {
        ["ORD-00482917"] = new OrderRecord
        {
            OrderId           = "ORD-00482917",
            CustomerEmail     = "sarah.chen@example.com",
            Status            = "shipped",
            Carrier           = "FedEx",
            TrackingNumber    = "794644792798",
            EstimatedDelivery = new DateOnly(2025, 8, 12),
            DeliveryDate      = null,
            Items =
            [
                new OrderItem("SKU-1042", "Wireless Headphones", 1, 89.99m)
            ]
        },
        ["ORD-00391045"] = new OrderRecord
        {
            OrderId           = "ORD-00391045",
            CustomerEmail     = "james.park@example.com",
            Status            = "delivered",
            Carrier           = "UPS",
            TrackingNumber    = "1Z999AA10123456784",
            EstimatedDelivery = new DateOnly(2025, 7, 28),
            DeliveryDate      = new DateOnly(2025, 7, 28),
            Items =
            [
                new OrderItem("SKU-2087", "Standing Desk Mat", 1, 45.00m)
            ]
        },
        ["ORD-00512334"] = new OrderRecord
        {
            OrderId           = "ORD-00512334",
            CustomerEmail     = "angry@example.com",
            Status            = "out_for_delivery",
            Carrier           = "DHL",
            TrackingNumber    = "1234567890",
            EstimatedDelivery = new DateOnly(2025, 8, 10),
            DeliveryDate      = null,
            Items =
            [
                new OrderItem("SKU-3301", "Mechanical Keyboard", 1, 129.99m)
            ]
        }
    };

    // ── Execute ──────────────────────────────────────────────────────────────
    public ToolResult Execute(string orderId, string customerEmail)
    {
        // Format guard (defence-in-depth — schema is primary)
        if (!Regex.IsMatch(orderId, @"^ORD-\d{8}$"))
            return ToolResult.Fail("invalid_order_id_format");

        if (!_ordersDb.TryGetValue(orderId, out var order))
            return ToolResult.Fail("order_not_found");

        // Ownership check — never expose PII without verification
        if (!order.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("email_mismatch");

        return ToolResult.Ok(new
        {
            order_id           = order.OrderId,
            status             = order.Status,
            carrier            = order.Carrier,
            tracking_number    = order.TrackingNumber,
            estimated_delivery = order.EstimatedDelivery.ToString("yyyy-MM-dd"),
            delivery_date      = order.DeliveryDate?.ToString("yyyy-MM-dd"),
            items              = order.Items.Select(i => new
            {
                sku        = i.Sku,
                name       = i.Name,
                quantity   = i.Qty,
                unit_price = i.Price
            })
        });
    }

    // Expose DB to other tools that need cross-reference
    public OrderRecord? GetById(string orderId) =>
        _ordersDb.TryGetValue(orderId, out var o) ? o : null;
}