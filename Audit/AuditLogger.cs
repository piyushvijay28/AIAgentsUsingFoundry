using System.Text.Json;

public static class AuditLogger
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "shopaxis_audit.jsonl");

    public static void LogToolCall(AuditEntry entry)
    {
        var line = JsonSerializer.Serialize(new
        {
            timestamp_utc = entry.TimestampUtc.ToString("O"),
            session_id    = entry.SessionId,
            thread_id     = entry.ThreadId,
            run_id        = entry.RunId,
            tool_name     = entry.ToolName,
            arguments     = JsonDocument.Parse(entry.Arguments).RootElement,
            result_status = ExtractStatus(entry.Result),
            latency_ms    = entry.LatencyMs,
            agent_version = "2.1"
        });

        File.AppendAllText(LogPath, line + Environment.NewLine);
        Console.WriteLine($"[AUDIT] {entry.ToolName} → {ExtractStatus(entry.Result)} ({entry.LatencyMs}ms)");
    }

    public static void LogSafetyEvent(string sessionId, string message, SafetyResult safety)
    {
        var line = JsonSerializer.Serialize(new
        {
            timestamp_utc  = DateTime.UtcNow.ToString("O"),
            session_id     = sessionId,
            event_type     = "content_safety_suspension",
            category       = safety.Category,
            max_severity   = safety.MaxSeverity,
            is_frustrated  = safety.IsFrustrated,
            message_length = message.Length   // never log raw message
        });

        File.AppendAllText(LogPath, line + Environment.NewLine);
        Console.WriteLine($"[SAFETY] Flagged — {safety.Category} severity {safety.MaxSeverity}");
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