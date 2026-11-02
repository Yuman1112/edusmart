using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Settings;
namespace SecureSmartTuitionSystem.Services.Security;

public sealed record SecurityExplanationResult(string Text, bool GeneratedByAI);
public sealed class SecurityExplanationAI(HttpClient http, IOptions<SecurityExplanationAISettings> options,
    ILogger<SecurityExplanationAI> logger)
{
    private readonly SecurityExplanationAISettings settings = options.Value;
    public bool IsConfigured => settings.Enabled && !string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.Model);
    public async Task<SecurityExplanationResult> ExplainAsync(AuditLogEntry entry, CancellationToken ct)
    {
        string local = SecurityActivityText.Summary(entry);
        if (!IsConfigured) return new(local, false);
        // Never send log values, names, identifiers, IP addresses, raw summaries or integrity proofs.
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new {
            model = settings.Model, store = false, max_output_tokens = 200,
            instructions = "Rewrite the supplied fixed system activity description as one or two short plain-English sentences for an administrator. Explain only the supplied meaning. Do not invent causes, attackers, breaches, successful payments or recommendations. Output plain text only.",
            input = local
        });
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) { logger.LogWarning("Security explanation provider returned status {Status}", (int)response.StatusCode); return new(local, false); }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var texts = new List<string>();
            if (json.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                foreach (var item in output.EnumerateArray())
                    if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                        foreach (var part in content.EnumerateArray())
                            if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var text)) texts.Add(text.GetString() ?? "");
            string result = string.Join(" ", texts).Trim();
            return result.Length is > 0 and <= 1000 ? new(result, true) : new(local, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning("Security explanation unavailable; using the local description.");
            return new(local, false);
        }
    }
}
