using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorAvailabilityService(ApplicationDbContext db)
{
    public async Task<List<int>> GetSlotsAsync(string userId) =>
        await db.TutorAvailabilitySlots.AsNoTracking()
            .Where(s => s.ApplicationUserId == userId)
            .Select(s => s.Day * 48 + s.Slot).ToListAsync();

    // End is exclusive. Partial half-hours must also be covered.
    public static bool Covers(IEnumerable<int> values, int day, TimeOnly start, TimeOnly end)
    {
        if (day < 0 || day > 6 || start >= end) return false;
        long halfHour = TimeSpan.TicksPerMinute * 30;
        int first = (int)(start.Ticks / halfHour);
        int last = (int)((end.Ticks + halfHour - 1) / halfHour);
        var slots = values.ToHashSet();
        return Enumerable.Range(first, last - first).All(s => slots.Contains(day * 48 + s));
    }

    public async Task<string?> ValidateAsync(string userId, DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if ((int)day < 0 || (int)day > 6 || start >= end)
            return "Choose a valid weekday and time range.";

        bool active = await db.TutorProfiles.AsNoTracking().AnyAsync(p =>
            p.ApplicationUserId == userId && p.IsActive && p.ApplicationUser.IsActive
            && !p.ApplicationUser.MustChangePassword);
        if (!active) return "The selected tutor account is not available for teaching.";

        var slots = await GetSlotsAsync(userId);
        if (slots.Count == 0)
            return "This tutor has no available teaching slots. Ask the tutor to save My Availability first.";
        return Covers(slots, (int)day, start, end) ? null
            : "The entire lesson must fall within the tutor's saved availability. Review Tutor Availability or choose another time.";
    }

    public static string DescribeDay(IEnumerable<int> values, int day)
    {
        var slots = values.Where(v => v >= day * 48 && v < (day + 1) * 48)
            .Select(v => v % 48).Distinct().Order().ToList();
        var ranges = new List<string>();
        for (int i = 0; i < slots.Count; i++)
        {
            int first = slots[i], last = first;
            while (i + 1 < slots.Count && slots[i + 1] == last + 1) last = slots[++i];
            ranges.Add($"{FormatMinutes(first * 30)}–{FormatMinutes((last + 1) * 30)}");
        }
        return ranges.Count == 0 ? "Not available" : string.Join(", ", ranges);
    }

    private static string FormatMinutes(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    public async Task<List<string>> FindExistingClassesOutsideAsync(string userId, IEnumerable<int> values)
    {
        var selected = values.ToHashSet();
        var today = ClassScheduleService.Today;
        var classes = await db.ClassSessions.AsNoTracking().Where(c =>
            c.TutorUserId == userId && c.Status == Models.Enrollment.ClassSessionStatus.Active).ToListAsync();
        var warnings = new List<string>();
        foreach (var c in classes)
        {
            var from = ClassScheduleService.FirstDate(c) > today ? ClassScheduleService.FirstDate(c) : today;
            var next = from.AddDays(((int)c.DayOfWeek - (int)from.DayOfWeek + 7) % 7);
            if (next.Add(c.EndTime.ToTimeSpan()) <= ClassScheduleService.Now) next = next.AddDays(7);
            if (next <= AcademicYearPolicy.ClassEnd(c)
                && !Covers(selected, (int)c.DayOfWeek, c.StartTime, c.EndTime))
                warnings.Add($"{c.ClassCode} ({c.DayOfWeek} {c.StartTime:HH:mm}–{c.EndTime:HH:mm})");
        }
        var replacements = await db.ClassLessonChanges.AsNoTracking()
            .Where(c => c.ClassSession.TutorUserId == userId && c.ReplacementDate >= today)
            .Select(c => new { c.ClassSession.ClassCode, c.ReplacementDate, c.ReplacementStartTime, c.ReplacementEndTime }).ToListAsync();
        foreach (var c in replacements)
        {
            if (c.ReplacementDate.HasValue && c.ReplacementStartTime.HasValue && c.ReplacementEndTime.HasValue
                && c.ReplacementDate.Value.Add(c.ReplacementEndTime.Value.ToTimeSpan()) > ClassScheduleService.Now
                && !Covers(selected, (int)c.ReplacementDate.Value.DayOfWeek, c.ReplacementStartTime.Value, c.ReplacementEndTime.Value))
                warnings.Add($"{c.ClassCode} (replacement {c.ReplacementDate:dd MMM yyyy} {c.ReplacementStartTime:HH:mm}–{c.ReplacementEndTime:HH:mm})");
        }
        return warnings.Distinct().ToList();
    }
}