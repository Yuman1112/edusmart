using System.Text.Json;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Services.Communication;
public sealed class ChatbotKnowledgeStore(IWebHostEnvironment environment, ILogger<ChatbotKnowledgeStore> logger)
{
    public List<ChatbotSource> All()
    {
        var result = FaqService.Entries.Select(x => new ChatbotSource("faq-" + x.Id, x.Question, x.Answer)).ToList();
        try
        {
            var path = Path.Combine(environment.ContentRootPath, "App_Data", "ChatbotKnowledge.json");
            if (File.Exists(path) && new FileInfo(path).Length <= 65536)
            {
                var entries = JsonSerializer.Deserialize<List<SupportArticle>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                foreach (var article in entries.Where(x => x.Published).Take(30))
                    if (System.Text.RegularExpressions.Regex.IsMatch(article.Id, "^[a-z0-9-]{1,40}$") && !string.IsNullOrWhiteSpace(article.Title) && article.Title.Length <= 150 && !string.IsNullOrWhiteSpace(article.Text) && article.Text.Length <= 1500)
                        result.Add(new("kb-" + article.Id, article.Title, article.Text));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { logger.LogWarning("Chatbot knowledge file unavailable; built-in FAQ remains available."); }
        return result.DistinctBy(x => x.Id).ToList();
    }
}
