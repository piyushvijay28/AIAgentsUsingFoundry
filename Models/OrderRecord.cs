using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class OrderRecord
{
    [Key]
    [MaxLength(20)]
    public string OrderId { get; set; } = "";

    [Required]
    [MaxLength(255)]
    public string CustomerEmail { get; set; } = "";

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "";

    [MaxLength(100)]
    public string Carrier { get; set; } = "";

    [MaxLength(100)]
    public string TrackingNumber { get; set; } = "";

    public DateOnly EstimatedDelivery { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    // Navigation property — EF Core loads related items
    public List<OrderItem> Items { get; set; } = [];
}