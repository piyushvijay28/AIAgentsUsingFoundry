using System.Text.Json;

public class ToolResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }

    public static ToolResult Ok(object data) =>
        new() { Success = true, Data = data };

    public static ToolResult Fail(string error) =>
        new() { Success = false, Error = error };

    // ✅ Required by Program.cs dispatcher
    public string ToJson() =>
        Success
            ? JsonSerializer.Serialize(Data)
            : JsonSerializer.Serialize(new { error = Error });
}