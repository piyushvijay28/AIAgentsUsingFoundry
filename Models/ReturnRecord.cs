using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ReturnRecord
{
    [Key]
    [MaxLength(20)]
    public string RmaNumber { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string OrderId { get; set; } = "";

    [MaxLength(50)]
    public string ReasonCode { get; set; } = "";

    [MaxLength(50)]
    public string RefundStage { get; set; } = "";

    [Column(TypeName = "decimal(10,2)")]
    public decimal RefundAmount { get; set; }

    [MaxLength(100)]
    public string PaymentMethod { get; set; } = "";

    [MaxLength(500)]
    public string LabelUrl { get; set; } = "";

    public DateOnly ExpectedCompletion { get; set; }

    public DateTime CreatedUtc { get; set; }

    // Navigation property — list of SKUs being returned
    public List<ReturnItem> ReturnItems { get; set; } = [];

    // Navigation back to order
    public OrderRecord Order { get; set; } = null!;

    // Helper — your tools use string[] ItemSkus
    // This converts the navigation property to string[]
    [NotMapped]
    public string[] ItemSkus
    {
        get => ReturnItems.Select(r => r.Sku).ToArray();
        set => ReturnItems = value.Select(s => new ReturnItem { Sku = s }).ToList();
    }
}