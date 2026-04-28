using Azure;
using Azure.AI.ContentSafety;

public class ContentSafetyEvaluator
{
    private readonly ContentSafetyClient _client;
    private const int SuspendThreshold = 4;

    public ContentSafetyEvaluator()
    {
        _client = new ContentSafetyClient(
            new Uri(Environment.GetEnvironmentVariable("CONTENT_SAFETY_ENDPOINT")!),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("CONTENT_SAFETY_KEY")!));
    }

    public async Task<SafetyResult> EvaluateAsync(string message)
    {
        var request = new AnalyzeTextOptions(message);
        request.Categories.Add(TextCategory.Hate);
        request.Categories.Add(TextCategory.Violence);
        request.Categories.Add(TextCategory.SelfHarm);
        request.Categories.Add(TextCategory.Sexual);

        var response = await _client.AnalyzeTextAsync(request);
        var analysis = response.Value.CategoriesAnalysis;

        var dominant  = analysis.OrderByDescending(c => c.Severity).First();
        bool suspend  = analysis.Any(c => c.Severity >= SuspendThreshold);

        var hateSeverity     = analysis.FirstOrDefault(c => c.Category == TextCategory.Hate)?.Severity ?? 0;
        var violenceSeverity = analysis.FirstOrDefault(c => c.Category == TextCategory.Violence)?.Severity ?? 0;

        bool isFrustrated = hateSeverity >= SuspendThreshold && violenceSeverity < SuspendThreshold;

        return new SafetyResult
        {
            ShouldSuspend = suspend,
            IsFrustrated  = isFrustrated,
            IsAbusive     = suspend && !isFrustrated,
            Category      = dominant.Category.ToString(),
            MaxSeverity   = dominant.Severity ?? 0,
            RawAnalysis   = analysis.ToDictionary(
                c => c.Category.ToString(),
                c => c.Severity ?? 0)
        };
    }
}