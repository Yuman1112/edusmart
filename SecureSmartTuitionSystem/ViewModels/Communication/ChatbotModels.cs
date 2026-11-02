namespace SecureSmartTuitionSystem.ViewModels.Communication;
public sealed record ChatbotSource(string Id, string Title, string Text, string? Url = null);
public sealed record ChatbotAnswer(string Text, bool IsAI, bool NeedsHuman, List<ChatbotSource> Sources);
public sealed record ChatbotTurn(string Question, string Answer, bool IsAI, bool NeedsHuman, List<ChatbotSource> Sources, bool IncludedAccountData);
public sealed record ChatbotHandoff(string UserId, string Question, string Answer, DateTime ExpiresAtUtc);
public sealed class SupportArticle
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string[] Keywords { get; set; } = [];
    public bool Published { get; set; }
}
