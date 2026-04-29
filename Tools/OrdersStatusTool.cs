using System.Text.RegularExpressions;

public class OrderStatusTool
{
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

    // ✅ All dates are NOW relative to today — always valid
    private readonly Dictionary<string, OrderRecord> _ordersDb;

    public OrderStatusTool()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _ordersDb = new Dictionary<string, OrderRecord>
        {
            ["ORD-00482917"] = new OrderRecord
            {
                OrderId           = "ORD-00482917",
                CustomerEmail     = "sarah.chen@example.com",
                Status            = "shipped",
                Carrier           = "FedEx",
                TrackingNumber    = "794644792798",
                EstimatedDelivery = today.AddDays(3),   // arriving in 3 days
                DeliveryDate      = null,
                Items             =
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
                EstimatedDelivery = today.AddDays(-5),  // estimated was 5 days ago
                DeliveryDate      = today.AddDays(-5),  // delivered 5 days ago
                Items             =
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
                EstimatedDelivery = today.AddDays(1),   // arriving tomorrow
                DeliveryDate      = null,
                Items             =
                [
                    new OrderItem("SKU-3301", "Mechanical Keyboard", 1, 129.99m)
                ]
            },
            ["ORD-00623891"] = new OrderRecord
            {
                OrderId           = "ORD-00623891",
                CustomerEmail     = "mike.ross@example.com",
                Status            = "processing",
                Carrier           = "FedEx",
                TrackingNumber    = "794644792900",
                EstimatedDelivery = today.AddDays(7),   // arriving in a week
                DeliveryDate      = null,
                Items             =
                [
                    new OrderItem("SKU-4412", "Ergonomic Chair", 1, 299.99m)
                ]
            },
            ["ORD-00734521"] = new OrderRecord
            {
                OrderId           = "ORD-00734521",
                CustomerEmail     = "lisa.wang@example.com",
                Status            = "delivered",
                Carrier           = "UPS",
                TrackingNumber    = "1Z999AA10987654321",
                EstimatedDelivery = today.AddDays(-20), // delivered 20 days ago
                DeliveryDate      = today.AddDays(-20), // within 30-day return window
                Items             =
                [
                    new OrderItem("SKU-5521", "USB-C Hub", 2, 35.00m),
                    new OrderItem("SKU-5522", "Laptop Stand", 1, 59.99m)
                ]
            }
        };
    }

    public ToolResult Execute(string orderId, string customerEmail)
    {
        if (!Regex.IsMatch(orderId, @"^ORD-\d{8}$"))
            return ToolResult.Fail("invalid_order_id_format");

        if (!_ordersDb.TryGetValue(orderId, out var order))
            return ToolResult.Fail("order_not_found");

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

    public OrderRecord? GetById(string orderId) =>
        _ordersDb.TryGetValue(orderId, out var o) ? o : null;
}