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
    public static DateTime BillingLimit(DateTime start, IEnumerable<ClassSession> classes) => classes.Select(ClassEnd).Append(EndOfYear(start)).Min();
    public static int MaximumMonths(DateTime start, DateTime limit)
    {
        limit = limit.Date < EndOfYear(start) ? limit.Date : EndOfYear(start);
        if (limit < start.Date) return 0;
        int months = (limit.Year - start.Year) * 12 + limit.Month - start.Month + 1;
        if (limit.Day < DateTime.DaysInMonth(limit.Year, limit.Month)) months--;
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
