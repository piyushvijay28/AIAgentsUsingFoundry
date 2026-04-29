using System.ComponentModel.DataAnnotations;

public class AuditEntry
{
    [Key]
    public int Id { get; set; }  // auto-increment

    [MaxLength(100)]
    public string SessionId { get; set; } = "";

    [MaxLength(100)]
    public string ThreadId { get; set; } = "";

    [MaxLength(100)]
    public string RunId { get; set; } = "";

    [MaxLength(100)]
    public string ToolName { get; set; } = "";

    // Store as JSON text — arguments can be long
    public string Arguments { get; set; } = "";

    public string Result { get; set; } = "";

    public int LatencyMs { get; set; }

    public DateTime TimestampUtc { get; set; }
}