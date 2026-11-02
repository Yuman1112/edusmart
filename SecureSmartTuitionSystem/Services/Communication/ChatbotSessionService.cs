using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Services.Communication;
public sealed class ChatbotSessionService(IDataProtectionProvider protection)
{
    private readonly IDataProtector state = protection.CreateProtector("EduSmart.Chatbot.Session.v1");
    private readonly IDataProtector handoff = protection.CreateProtector("EduSmart.Chatbot.Handoff.v1");
    private sealed record History(string UserId, List<ChatbotTurn> Turns);
    public List<ChatbotTurn> Read(ISession session, string userId)
    {
        try
        {
            var raw = session.GetString("Chatbot.History");
            if (raw == null) return [];
            var saved = JsonSerializer.Deserialize<History>(state.Unprotect(raw));
            return saved?.UserId == userId ? saved.Turns.Where(x => !x.IncludedAccountData && x.Sources.All(s => s.Id.StartsWith("faq-") || s.Id.StartsWith("kb-"))).TakeLast(6).ToList() : [];
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return []; }
    }
    public void Save(ISession session, string userId, List<ChatbotTurn> turns) => session.SetString("Chatbot.History", state.Protect(JsonSerializer.Serialize(new History(userId, turns.TakeLast(6).ToList()))));
    public string IssueHandoff(string userId, ChatbotTurn turn) => handoff.Protect(JsonSerializer.Serialize(new ChatbotHandoff(userId, turn.Question, turn.Answer.Length > 1200 ? turn.Answer[..1200] : turn.Answer, DateTime.UtcNow.AddMinutes(20))));
    public ChatbotHandoff? ReadHandoff(string? token, string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(token) || token.Length > 12000) return null;
            var data = JsonSerializer.Deserialize<ChatbotHandoff>(handoff.Unprotect(token));
            return data != null && data.UserId == userId && data.ExpiresAtUtc > DateTime.UtcNow ? data : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }
}
