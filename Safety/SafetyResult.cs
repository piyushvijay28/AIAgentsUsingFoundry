public class SafetyResult
{
    public bool ShouldSuspend { get; set; }
    public bool IsFrustrated { get; set; }
    public bool IsAbusive { get; set; }
    public string Category { get; set; } = "";
    public int MaxSeverity { get; set; }
    public Dictionary<string, int> RawAnalysis { get; set; } = [];
}