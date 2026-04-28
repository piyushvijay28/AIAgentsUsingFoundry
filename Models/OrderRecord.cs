public record OrderItem(string Sku, string Name, int Qty, decimal Price);

public class OrderRecord
{
    public string OrderId { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public string Carrier { get; set; } = "";
    public string TrackingNumber { get; set; } = "";
    public DateOnly EstimatedDelivery { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public OrderItem[] Items { get; set; } = [];
}
