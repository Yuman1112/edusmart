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
        new FaqEntry("monthly-lessons", "How do monthly lessons and partial-month enrollment work?",
            "Regular weekly classes run from the 1st to the 28th, giving four scheduled lessons per complete month. The 29th–31st are centre holidays, except separately notified replacement lessons. Each selected class is checked separately against its own weekday and term dates. If only one or two regular lessons remain in the start month, review their dates and confirm the partial-month enrollment. If a selected class has no remaining regular lesson that month, change the start date or remove that class. The existing full monthly tuition still applies to a partial first month; replacement lessons follow separate eligibility.",
            ["monthly lessons", "four lessons", "4 lessons", "last two lessons", "final two lessons", "partial-month", "29th", "30th", "31st", "补习假期", "最后两堂"]),
        new FaqEntry("support", "How can I speak to a staff member?",
            "Guests can choose Contact staff without signing in, then leave their name, email and question. Staff can reply by email. Signed-in families can use Messages for account support. Staff can help with questions that are not covered here.",
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
