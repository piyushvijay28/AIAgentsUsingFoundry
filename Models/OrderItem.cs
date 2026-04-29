using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class OrderItem
{
    [Key]
    public int Id { get; set; }  // EF Core auto-increment PK

    [Required]
    [MaxLength(20)]
    public string OrderId { get; set; } = "";  // FK to Orders

    [MaxLength(20)]
    public string Sku { get; set; } = "";

    [MaxLength(255)]
    public string Name { get; set; } = "";

    public int Qty { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    // Navigation property back to order
    public OrderRecord Order { get; set; } = null!;

    // Parameterless constructor for EF Core
    public OrderItem() { }

    // Keep your existing constructor
    public OrderItem(string sku, string name, int qty, decimal price)
    {
        Sku = sku; Name = name; Qty = qty; Price = price;
    }
}