using System.Text.RegularExpressions;
namespace SecureSmartTuitionSystem.Services.Communication;

// Conservative screening supplements server-side data separation; it is not a full DLP engine.
public static class ChatbotPrivacyPolicy
{
    public static bool IsSensitive(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Normalize(System.Text.NormalizationForm.FormKC);
        return Regex.IsMatch(value, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}|\bsk-[A-Za-z0-9_-]+|(?:\d[\s()+-]*){7,}|(?:password|pwd|secret|token|api[ _-]?key)\s*[:=]|(?:server|data source|initial catalog)\s*=", RegexOptions.IgnoreCase)
            || new[] { "other student", "another student", "other parent", "another family", "all students", "student list", "system prompt", "connection string", "smtp password", "api key", "private note", "security log", "其他学生", "别人的", "所有学生", "身份证", "银行卡", "信用卡", "密钥", "系统提示", "安全日志", "内部备注" }.Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));
    }
    public static string ForHistory(string question) => IsSensitive(question) ? "[Confidential question omitted. Please use secure account pages or Messages.]" : question;
}
