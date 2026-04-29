using System.ComponentModel.DataAnnotations;

public class ReturnItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string RmaNumber { get; set; } = "";

    [MaxLength(20)]
    public string Sku { get; set; } = "";

    // Navigation back to return
    public ReturnRecord Return { get; set; } = null!;
}