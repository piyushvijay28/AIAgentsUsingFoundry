public class ReturnRecord
{
    public string RmaNumber { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string[] ItemSkus { get; set; } = [];
    public string RefundStage { get; set; } = "";
    public decimal RefundAmount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string LabelUrl { get; set; } = "";
    public DateOnly ExpectedCompletion { get; set; }
    public DateTime CreatedUtc { get; set; }
}