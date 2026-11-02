using System.Security.Claims;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Services.Communication;
public sealed class ChatbotAssistantService(ChatbotAIService ai, ChatbotKnowledgeStore knowledge, ChatbotContextService data)
{
    public bool AIConfigured => ai.IsConfigured;
    public static bool RequiresStaff(string question) => new[] { "human", "staff member", "complaint", "approve refund", "refund me", "can i get a refund", "refund amount", "pay for me", "withdraw for me", "能退款", "可以退款", "退多少", "退款金额", "帮我退课", "帮我付款", "人工", "真人", "投诉", "批准退款", "退钱", "特批" }.Any(x => question.Contains(x, StringComparison.OrdinalIgnoreCase));
    public async Task<ChatbotAnswer> AskAsync(ClaimsPrincipal user, string question, string scope, bool includeAccount, List<ChatbotTurn> history, CancellationToken ct)
    {
        if (ChatbotPrivacyPolicy.IsSensitive(question)) return new("Please do not enter confidential details here. Use the secure account pages or contact the centre through Messages. This question was not sent to AI.", false, true, []);
        if (RequiresStaff(question)) return new("This request needs a staff member. You can review a support-message draft below.", false, true, []);
        var inferred = ChatbotContextService.ResolveScope(question, "auto");
        scope = inferred;
        if (scope == "centre" && inferred is "bills" or "enrollments") scope = inferred;
        bool privateQuery = scope is "bills" or "enrollments";
        if (privateQuery && user.Identity?.IsAuthenticated != true) return new("Please sign in to view your own bills or enrolled classes. Guests can ask general questions and browse public class availability.", false, false, []);
        if (privateQuery && (!user.IsInRole("Parent") && !user.IsInRole("Student"))) return new("Family-account queries are available only to the signed-in guardian or student. Staff should use the normal management pages.", false, true, []);

        var sources = knowledge.All();
        var live = await data.GetAsync(user, scope, question, ct);
        if (privateQuery || scope == "available") sources.AddRange(live);
        if (privateQuery)
        {
            var references = System.Text.RegularExpressions.Regex.Matches(question, @"\b(?:ST|PA|TU|BL|MB|EA)\d+\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Select(x => x.Value).ToList();
            if (references.Any(reference => !string.Equals(reference, user.Identity?.Name, StringComparison.OrdinalIgnoreCase) &&
                !live.Any(x => (x.Title + " " + x.Text).Contains(reference, StringComparison.OrdinalIgnoreCase))))
                return new("That reference was not returned for your permitted account. I cannot look up another family's records. Open your account pages or contact staff to verify it.", false, true, []);
        }
        // Account and class records stay on this server; do not put them in an AI request.
        if (privateQuery || scope == "available")
            return new("These records were retrieved by EduSmart for your permitted account or public class search. They were not sent to AI. Open the linked pages for the full list.", false, false, live);
        var result = await ai.AnswerAsync(question, sources, [], ct);
        if (result != null && (result.NeedsHuman || (!privateQuery && scope != "available") || result.Sources.Any(x => live.Any(y => x.Id == y.Id)))) return result;
        if (privateQuery || scope == "available")
            return new("Here are the website records available for this query. This preview can be incomplete; open the linked pages for the full list. AI is not enabled or did not return a verified response.", false, false, live.Take(20).ToList());
        var faq = FaqService.Match(question);
        if (faq != null) return new(faq.Answer, false, false, sources.Where(x => x.Id == "faq-" + faq.Id).ToList());
        return new("I could not find a supported answer in the centre information. Please send this question to a staff member.", false, true, []);
    }
}
