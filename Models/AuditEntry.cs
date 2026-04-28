public class AuditEntry
{
    public string SessionId { get; set; } = "";
    public string ThreadId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string Result { get; set; } = "";
    public int LatencyMs { get; set; }
    public DateTime TimestampUtc { get; set; }
}