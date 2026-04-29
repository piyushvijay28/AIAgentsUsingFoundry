using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

public class OrderStatusTool
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
        }
      },
      "required": ["order_id", "customer_email"],
      "additionalProperties": false
    }
    """;

    private readonly ShopAxisDbContext _db;

    public OrderStatusTool(ShopAxisDbContext db)
    {
        _db = db;
    }

    public ToolResult Execute(string orderId, string customerEmail)
    {
        if (!Regex.IsMatch(orderId, @"^ORD-\d{8}$"))
            return ToolResult.Fail("invalid_order_id_format");

        // EF Core query — includes related items
        var order = _db.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.OrderId == orderId);

        if (order == null)
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
        _db.Orders
           .Include(o => o.Items)
           .FirstOrDefault(o => o.OrderId == orderId);
}