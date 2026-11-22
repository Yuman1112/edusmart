namespace SecureSmartTuitionSystem.Services.Attendance;

public static class AttendanceEntryPolicy
{
    /*
     * OWASP TOP 10 | A04:2021 / A06:2025
     * Editing requires unsubmitted attendance and an open entry window.
     * The normal deadline is lesson end + 24 hours; approved reopening may extend it.
     */
    public static DateTime LessonEnd(DateTime date, TimeOnly end) =>
        date.Date.Add(end.ToTimeSpan());

    public static DateTime Deadline(
        DateTime date,
        TimeOnly end,
        DateTime? openUntilUtc = null)
    {
        var normal = LessonEnd(date, end).AddHours(24);

        return openUntilUtc.HasValue && openUntilUtc.Value.AddHours(8) > normal
            ? openUntilUtc.Value.AddHours(8)
            : normal;
    }

    public static bool CanEdit(
        DateTime date,
        TimeOnly start,
        TimeOnly end,
        DateTime? submitted,
        DateTime now,
        DateTime? openUntilUtc = null) =>
        submitted == null
        && start < end
        && now >= date.Date.Add(start.ToTimeSpan())
        && now < Deadline(date, end, openUntilUtc);

    public static string? ReasonError(
        string? reason,
        bool finalSubmit,
        bool late) =>
        reason?.Length > 500
            ? "Late submission reason must not exceed 500 characters."
            : finalSubmit && late && string.IsNullOrWhiteSpace(reason)
                ? "Enter a late submission reason before submitting."
                : null;

    public static bool IsLate(DateTime date, TimeOnly end, DateTime now) =>
        now >= LessonEnd(date, end);
}
