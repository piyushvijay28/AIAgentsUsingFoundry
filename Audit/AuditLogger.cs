using System.Text.Json;

public static class AuditLogger
{
    private static ShopAxisDbContext? _db;

    // Call this once from Program.cs after DbContext is created
    public static void Initialize(ShopAxisDbContext db) => _db = db;

    public static void LogToolCall(AuditEntry entry)
    {
        _db?.AuditLog.Add(entry);
        _db?.SaveChanges();

        // Also keep console output
        Console.WriteLine(
            $"[AUDIT] {entry.ToolName} → {ExtractStatus(entry.Result)} ({entry.LatencyMs}ms)");
    }

    public static void LogSafetyEvent(string sessionId, string message, SafetyResult safety)
    {
        _db?.AuditLog.Add(new AuditEntry
        {
            SessionId    = sessionId,
            ThreadId     = "",
            RunId        = "",
            ToolName     = "content_safety_suspension",
            Arguments    = $"{{\"category\":\"{safety.Category}\"," +
                           $"\"severity\":{safety.MaxSeverity}}}",
            Result       = safety.IsFrustrated ? "frustrated" : "abusive",
            LatencyMs    = 0,
            TimestampUtc = DateTime.UtcNow
        });
        _db?.SaveChanges();

        Console.WriteLine(
            $"[SAFETY] Flagged — {safety.Category} severity {safety.MaxSeverity}");
    }

    private static string ExtractStatus(string resultJson)
    {
        try
        {
            var doc = JsonDocument.Parse(resultJson);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.ValueKind != JsonValueKind.Null)
                return err.GetString() ?? "error";
            return "success";
        }
        catch { return "parse_error"; }
    }
}