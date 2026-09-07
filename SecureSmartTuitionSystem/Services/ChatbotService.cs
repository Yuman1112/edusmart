using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class ChatbotService : IChatbotService
{
    private readonly AppDbContext _db;

    public ChatbotService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChatbotResponse> AnswerByMenuSelectionAsync(int chatbotFaqId, string askingUserId)
    {
        var faq = await _db.ChatbotFaqs
            .Include(f => f.Keywords)
            .FirstOrDefaultAsync(f => f.ChatbotFaqId == chatbotFaqId && f.IsActive);

        if (faq is null)
        {
            return new ChatbotResponse { IsMatch = false };
        }

        _db.ChatbotLogs.Add(new ChatbotLog
        {
            UserId = askingUserId,
            QuestionText = $"[menu:{faq.Question}]",
            MatchedChatbotFaqId = faq.ChatbotFaqId
        });
        await _db.SaveChangesAsync();

        return new ChatbotResponse
        {
            IsMatch = true,
            MatchedFaq = faq,
            AnswerText = faq.Answer,
            SuggestedQuestions = await GetRelatedQuestionsAsync(faq)
        };
    }

    public async Task<ChatbotResponse> AnswerByFreeTextAsync(string questionText, string askingUserId)
    {
        var normalized = (questionText ?? string.Empty).Trim().ToLowerInvariant();

        var faqsWithKeywords = await _db.ChatbotFaqs
            .Where(f => f.IsActive)
            .Include(f => f.Keywords)
            .ToListAsync();

        ChatbotFaq? bestMatch = null;
        var bestMatchedKeywordCount = 0;

        foreach (var faq in faqsWithKeywords)
        {
            var anyKeywords = faq.Keywords.Where(k => k.MatchMode == "Any").ToList();
            var allKeywords = faq.Keywords.Where(k => k.MatchMode == "All").ToList();

            // "All" category: every keyword tagged All for this FAQ must appear (Lokman & Zain 2010).
            var allSatisfied = allKeywords.Count == 0 ||
                                allKeywords.All(k => normalized.Contains(k.Keyword.ToLowerInvariant()));

            if (!allSatisfied) continue;

            // "Any" category: a single matching keyword is enough.
            var anyMatchedCount = anyKeywords.Count(k => normalized.Contains(k.Keyword.ToLowerInvariant()));

            var totalRelevance = anyMatchedCount + (allKeywords.Count > 0 ? allKeywords.Count : 0);
            if (totalRelevance == 0) continue;

            if (totalRelevance > bestMatchedKeywordCount)
            {
                bestMatchedKeywordCount = totalRelevance;
                bestMatch = faq;
            }
        }

        _db.ChatbotLogs.Add(new ChatbotLog
        {
            UserId = askingUserId,
            QuestionText = questionText ?? string.Empty,
            MatchedChatbotFaqId = bestMatch?.ChatbotFaqId,
            WasEscalated = bestMatch is null
        });
        await _db.SaveChangesAsync();

        if (bestMatch is null)
        {
            // Table 4.14, Alternative Flow A1: "No exact match found for your query."
            return new ChatbotResponse { IsMatch = false };
        }

        return new ChatbotResponse
        {
            IsMatch = true,
            MatchedFaq = bestMatch,
            AnswerText = bestMatch.Answer,
            SuggestedQuestions = await GetRelatedQuestionsAsync(bestMatch)
        };
    }

    public async Task EscalateAsync(string questionText, string askingUserId)
    {
        _db.ChatbotLogs.Add(new ChatbotLog
        {
            UserId = askingUserId,
            QuestionText = questionText ?? string.Empty,
            WasEscalated = true
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Suggests other active FAQs from the same menu category (Table 4.14 step 6).</summary>
    private async Task<List<ChatbotFaq>> GetRelatedQuestionsAsync(ChatbotFaq faq)
    {
        return await _db.ChatbotFaqs
            .Where(f => f.IsActive && f.MenuCategory == faq.MenuCategory && f.ChatbotFaqId != faq.ChatbotFaqId)
            .OrderBy(f => f.DisplayOrder)
            .Take(3)
            .ToListAsync();
    }
}
