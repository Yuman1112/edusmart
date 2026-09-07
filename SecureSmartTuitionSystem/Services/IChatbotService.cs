using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class ChatbotResponse
{
    public bool IsMatch { get; set; }
    public ChatbotFaq? MatchedFaq { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public List<ChatbotFaq> SuggestedQuestions { get; set; } = new();
}

/// <summary>
/// Rule-based, menu-driven chatbot supplemented by a keyword-pattern
/// matching layer (Ch.2.3 "Adopted Approach and Implementation",
/// Communication &amp; Announcement Module). The two layers are retained
/// together rather than one replacing the other: the menu gives fast,
/// guaranteed-accurate answers for well-known categories, while the keyword
/// layer lets a guardian who types a free-text question still get a
/// relevant answer instead of only a "no match" fallback.
/// </summary>
public interface IChatbotService
{
    /// <summary>Answers by an exact menu selection (a specific FAQ id was clicked).</summary>
    Task<ChatbotResponse> AnswerByMenuSelectionAsync(int chatbotFaqId, string askingUserId);

    /// <summary>
    /// Answers free-typed text using one-match/all-match keyword categories
    /// (Lokman &amp; Zain 2010, cited Ch.2.3). Escalates (IsMatch = false) when
    /// no keyword rule fires, per Table 4.14 alternative flow A1.
    /// </summary>
    Task<ChatbotResponse> AnswerByFreeTextAsync(string questionText, string askingUserId);

    /// <summary>Records an unresolved enquiry as escalated to a human admin (Functional Requirements: "escalate...to a human staff member").</summary>
    Task EscalateAsync(string questionText, string askingUserId);
}
