using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public static class AcademicYearPolicy
{
    public static DateTime EndOfYear(DateTime start) => new(start.Year, 12, 31);
    public static DateTime ClassEnd(ClassSession c)
    {
        var yearEnd = EndOfYear(ClassScheduleService.FirstDate(c));
        return c.EndDate.HasValue && c.EndDate.Value.Date < yearEnd ? c.EndDate.Value.Date : yearEnd;
    }
    public static DateTime BillingLimit(DateTime start, IEnumerable<ClassSession> classes) => classes.Select(BillableEnd).Append(EndOfYear(start)).Min();
    // A complete teaching month ends with the fourth regular weekly lesson,
    // not with the 29th–31st centre holidays. A genuinely shortened final month
    // is still excluded; this does not introduce a new prorated fee policy.
    public static DateTime BillableEnd(ClassSession c)
    {
        var end = ClassEnd(c);
        var last = new DateTime(end.Year, end.Month, 28);
        last = last.AddDays(-((int)last.DayOfWeek - (int)c.DayOfWeek + 7) % 7);
        return end >= last ? new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month))
            : new DateTime(end.Year, end.Month, 1).AddDays(-1);
    }
    // Enrollment is monthly: the requested date may precede the first weekly
    // lesson within its opening month. Actual lessons still use ValidOn/FirstDate.
    public static bool CanEnrollOn(ClassSession c, DateTime start)
    {
        var first = ClassScheduleService.FirstDate(c);
        return c.Status == ClassSessionStatus.Active && start.Year == first.Year &&
            start.Date >= new DateTime(first.Year, first.Month, 1) && start.Date <= ClassEnd(c);
    }
    // Editing a class must use the same teaching-month boundary as enrollment.
    public static bool CoversPlan(ClassSession proposed, DateTime start, int months) =>
        months is >= 1 and <= 12 && CanEnrollOn(proposed, start) && BillableEnd(proposed) >= new DateTime(start.Year, start.Month, 1).AddMonths(months).AddDays(-1);
    public static int MaximumMonths(DateTime start, DateTime limit)
    {
        limit = limit.Date < EndOfYear(start) ? limit.Date : EndOfYear(start);
        if (limit < start.Date) return 0;
        int months = (limit.Year - start.Year) * 12 + limit.Month - start.Month + 1;
        if (limit.Day < 28) months--;
        return Math.Clamp(months, 0, 12);
    }
    public static List<int> AllowedPlans(int maximum) => new[] { 1, 3, 6, 9, 12 }.Where(x => x <= maximum).Concat(maximum == 2 ? new[] { 2 } : Array.Empty<int>()).OrderBy(x => x).ToList();
    public static string? Validate(DateTime start, int months, IEnumerable<ClassSession> classes)
    {
        if (start.Year < 2000 || start.Year > 2100) return "Choose a valid academic year (2000–2100).";
        int maximum = MaximumMonths(start, BillingLimit(start, classes));
        return AllowedPlans(maximum).Contains(months) ? null : $"This start date allows at most {maximum} prepaid month(s), ending within the class term and 31 December. Select an available plan.";
    }
}
