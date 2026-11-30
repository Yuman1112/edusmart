namespace SecureSmartTuitionSystem.Services.Enrollment;

// Four weekly regular lessons per complete month. Replacements are separate entitlements.
public static class LateMonthEnrollmentPolicy
{
    public static bool IsRegularTeachingDate(DateTime date) => date.Day <= 28;

    public static bool HasRegularLessonBetween(DateTime from, DateTime to, DayOfWeek day)
    {
        for (var date = from.Date.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);
             date <= to.Date; date = date.AddDays(7))
            if (IsRegularTeachingDate(date)) return true;
        return false;
    }

    public static List<DateTime> RemainingDates(DateTime start, DayOfWeek day,
        DateTime termStart, DateTime termEnd, TimeOnly? lessonStart = null, DateTime? now = null, IEnumerable<DateTime>? cancelled = null)
    {
        var dates = new List<DateTime>();
        var excluded = (cancelled ?? []).Select(x => x.Date).ToHashSet();
        for (int d = 1; d <= 28; d++)
        {
            var date = new DateTime(start.Year, start.Month, d);
            if (date < start.Date || date < termStart.Date || date > termEnd.Date || date.DayOfWeek != day || excluded.Contains(date)) continue;
            // A lesson already started today cannot be joined through a new application.
            if (now.HasValue && lessonStart.HasValue && date.Add(lessonStart.Value.ToTimeSpan()) <= now.Value) continue;
            dates.Add(date);
        }
        return dates;
    }
}
