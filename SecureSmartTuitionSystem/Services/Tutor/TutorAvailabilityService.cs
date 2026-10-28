using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorAvailabilityService(ApplicationDbContext db)
{
    public async Task<List<int>> GetStoredSlotsAsync(string userId) =>
        await db.TutorAvailabilitySlots.AsNoTracking()
            .Where(s => s.ApplicationUserId == userId)
            .Select(s => s.Day * 48 + s.Slot).ToListAsync();

    public async Task<List<int>> GetSlotsAsync(string userId)
    {
        var type = await db.TutorProfiles.AsNoTracking().Where(p => p.ApplicationUserId == userId)
            .Select(p => (Models.Tutor.TutorEmploymentType?)p.EmploymentType).SingleOrDefaultAsync();
        if (type == Models.Tutor.TutorEmploymentType.FullTime) return TutorAvailabilityPolicy.FullTimeSlots();
        if (type != Models.Tutor.TutorEmploymentType.PartTime) return new();
        return (await GetStoredSlotsAsync(userId)).Where(TutorAvailabilityPolicy.IsOpen).ToList();
    }

    // Caller owns the transaction and SaveChanges.
    public async Task SyncFullTimeSlotsAsync(string userId)
    {
        var expected = TutorAvailabilityPolicy.FullTimeSlots().ToHashSet();
        var stored = await db.TutorAvailabilitySlots.Where(x => x.ApplicationUserId == userId).ToListAsync();
        var original = stored.Select(x => x.Day * 48 + x.Slot).ToHashSet();
        db.TutorAvailabilitySlots.RemoveRange(stored.Where(x => !expected.Contains(x.Day * 48 + x.Slot)));
        foreach (var value in expected.Except(original))
            db.TutorAvailabilitySlots.Add(new Models.Tutor.TutorAvailabilitySlot
            {
                ApplicationUserId = userId,
                Day = value / 48,
                Slot = value % 48
            });
    }

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

        if ((int)day == 0 || start < new TimeOnly(8, 0) || end > new TimeOnly(22, 0))
            return "Lessons must be within Monday–Saturday, 08:00–22:00 MYT.";

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
        var classes = await db.ClassSessions.AsNoTracking().Where(c => c.TutorUserId == userId).ToListAsync();
        if (classes.Count == 0) return new();
        var latestReplacement = await db.ClassLessonChanges.AsNoTracking()
            .Where(x => x.ClassSession.TutorUserId == userId && x.ReplacementDate >= today)
            .MaxAsync(x => x.ReplacementDate);
        var end = classes.Where(c => c.Status == Models.Enrollment.ClassSessionStatus.Active)
            .Select(AcademicYearPolicy.ClassEnd).Append(today).Max();
        if (latestReplacement.HasValue && latestReplacement.Value.Date > end) end = latestReplacement.Value.Date;
        var lessons = await new ClassScheduleService(db).GetLessonsAsync(today, end, userId);
        return lessons.Where(x => !x.Cancelled && x.Date.Add(x.End.ToTimeSpan()) > ClassScheduleService.Now
                && !Covers(selected, (int)x.Date.DayOfWeek, x.Start, x.End))
            .Select(x => x.Replacement
                ? $"{x.Class.ClassCode} (replacement {x.Date:dd MMM yyyy} {x.Start:HH:mm}–{x.End:HH:mm})"
                : $"{x.Class.ClassCode} ({x.Date.DayOfWeek} {x.Start:HH:mm}–{x.End:HH:mm})")
            .Distinct().ToList();
    }
}
