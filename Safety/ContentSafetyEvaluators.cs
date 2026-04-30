using Azure;
using Azure.AI.ContentSafety;

public class ContentSafetyEvaluator
{
    private readonly ContentSafetyClient _client;
    private const int SuspendThreshold = 3;

    public ContentSafetyEvaluator()
    {
        _client = new ContentSafetyClient(
            new Uri(Environment.GetEnvironmentVariable("CONTENT_SAFETY_ENDPOINT")!),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("CONTENT_SAFETY_KEY")!));
    }

    // Local keywords that always trigger suspension regardless of Azure scores
private static readonly string[] AbusiveKeywords =
[
    "destroy", "kill", "threaten", "regret", "useless idiots",
    "i will make you", "swear", "lawsuit", "hack", "attack"
];

private static readonly string[] FrustratedKeywords =
[
    "fed up", "unacceptable", "ridiculous", "outrageous",
    "terrible", "awful", "disgusting", "worst", "horrible",
    "still hasn't arrived", "been waiting", "no one is helping"
];

public async Task<SafetyResult> EvaluateAsync(string message)
{
    var lower = message.ToLower();

    // ── Azure Content Safety check ────────────────────────────────────
    var request = new AnalyzeTextOptions(message);
    request.Categories.Add(TextCategory.Hate);
    request.Categories.Add(TextCategory.Violence);
    request.Categories.Add(TextCategory.SelfHarm);
    request.Categories.Add(TextCategory.Sexual);

    var response = await _client.AnalyzeTextAsync(request);
    var analysis = response.Value.CategoriesAnalysis;

    // Console.WriteLine("\n[SAFETY SCORES]");
    // foreach (var category in analysis)
    //     Console.WriteLine($"  {category.Category}: {category.Severity}");

    var hateSeverity = analysis
        .FirstOrDefault(c => c.Category == TextCategory.Hate)?.Severity ?? 0;
    var violSeverity = analysis
        .FirstOrDefault(c => c.Category == TextCategory.Violence)?.Severity ?? 0;
    var dominant = analysis.OrderByDescending(c => c.Severity).First();

    // ── Local keyword detection ───────────────────────────────────────
    bool hasAbusiveKeyword   = AbusiveKeywords.Any(k => lower.Contains(k));
    bool hasFrustratedKeyword = FrustratedKeywords.Any(k => lower.Contains(k));

    // ── Combine both signals ──────────────────────────────────────────
    bool azureSuspend    = analysis.Any(c => c.Severity >= SuspendThreshold);
    bool azureFrustrated = hateSeverity >= SuspendThreshold && violSeverity < SuspendThreshold;

    // Local keywords override if Azure scores are low
    bool shouldSuspend  = azureSuspend || hasAbusiveKeyword;
    bool isFrustrated   = !hasAbusiveKeyword && (azureFrustrated || hasFrustratedKeyword);

    // Console.WriteLine($"  AzureSuspend:    {azureSuspend}");
    // Console.WriteLine($"  KeywordAbusive:  {hasAbusiveKeyword}");
    // Console.WriteLine($"  KeywordFrustrate:{hasFrustratedKeyword}");
    // Console.WriteLine($"  ShouldSuspend:   {shouldSuspend}");
    // Console.WriteLine($"  IsFrustrated:    {isFrustrated}\n");

    return new SafetyResult
    {
        ShouldSuspend = shouldSuspend,
        IsFrustrated  = isFrustrated,
        IsAbusive     = shouldSuspend && !isFrustrated,
        Category      = hasAbusiveKeyword ? "LocalKeyword" : dominant.Category.ToString(),
        MaxSeverity   = dominant.Severity ?? 0,
        RawAnalysis   = analysis.ToDictionary(
            c => c.Category.ToString(),
            c => c.Severity ?? 0)
    };
}
}