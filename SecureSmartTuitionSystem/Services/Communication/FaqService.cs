namespace SecureSmartTuitionSystem.Services.Communication;

public sealed record FaqEntry(string Id, string Question, string Answer, string[] Keywords);

/// <summary>Deterministic, centre-maintained answers; no generative AI is used.</summary>
public static class FaqService
{
    public static readonly IReadOnlyList<FaqEntry> Entries = Array.AsReadOnly(new[]
    {
        new FaqEntry("enrollment", "How do I enroll in a class?",
            "Browse available classes and submit an enrollment request. The centre reviews the request before providing payment instructions. Existing families should sign in first.",
            ["enroll", "enrol", "register", "registration", "sign up", "报名", "注册"]),
        new FaqEntry("payment", "Where can I find and pay a bill?",
            "Open Payment & Billing to see bills available to your account. Follow the payment instructions shown for an eligible unpaid bill. For an initial application, use the link sent by the centre.",
            ["payment", "pay", "bill", "invoice", "fee", "付款", "缴费", "账单"]),
        new FaqEntry("receipt", "Where can I see my receipt?",
            "Open Payment & Billing and select a completed payment or receipt. If a payment is not yet confirmed, contact the centre before trying to pay again.",
            ["receipt", "payment history", "transaction history", "收据", "付款记录"]),
        new FaqEntry("schedule", "Where can I check class schedules?",
            "Open the classes or schedule page to view available class details. Contact the centre if you need help with a schedule change or class availability.",
            ["schedule", "timetable", "class time", "class", "课程", "时间表", "上课"]),
        new FaqEntry("support", "How can I speak to a staff member?",
            "Use Messages to start or continue a conversation with the centre. Staff can help with questions that are not covered here.",
            ["human", "staff", "contact", "support", "help", "admin", "人工", "客服", "帮助"])
    });

    public static FaqEntry? FindById(string? id) =>
        Entries.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

    public static FaqEntry? Match(string? question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;
        string normalized = question.Trim().Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant();
        // More specific phrases such as "payment history" beat a broad "payment" match.
        return Entries.SelectMany((entry, order) => entry.Keywords.Select(keyword => new { entry, order, keyword }))
            .Where(x => normalized.Contains(x.keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.keyword.Length).ThenBy(x => x.order)
            .Select(x => x.entry).FirstOrDefault();
    }
}
