using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorDatedAvailabilityService(ApplicationDbContext db)
{
    private (string Tutor, DateTime From, DateTime To)? recurringKey;
    private List<int> recurringNormal = [];
    private List<TutorAvailabilityAdjustment> recurringChanges = [];
    public static List<int> Range(DateTime date, TimeOnly start, TimeOnly end)
    {
        if (start >= end) return [];
        int first = (int)(start.Ticks / TimeSpan.FromMinutes(30).Ticks);
        int last = (int)((end.Ticks + TimeSpan.FromMinutes(30).Ticks - 1) / TimeSpan.FromMinutes(30).Ticks);
        return Enumerable.Range(first, last - first).Select(slot => (int)date.DayOfWeek * 48 + slot).ToList();
    }
    public static bool Aligned(TimeOnly time) => time.Ticks % TimeSpan.FromMinutes(30).Ticks == 0;
    public async Task<List<TutorAvailabilityAdjustment>> AdjustmentsAsync(string tutorId, DateTime from, DateTime to) =>
        await db.TutorAvailabilityAdjustments.AsNoTracking().Include(x => x.ClassLessonChange)
            .Where(x => x.TutorUserId == tutorId &&
                ((x.BlocksOriginal && x.ClassLessonChange.OriginalDate >= from && x.ClassLessonChange.OriginalDate <= to)
                || (x.AddedDate >= from && x.AddedDate <= to))).ToListAsync();

    public static List<int> Apply(IEnumerable<int> normal, DateTime week, IEnumerable<TutorAvailabilityAdjustment> adjustments)
    {
        week = TutorAvailabilityPolicy.WeekStart(week);
        var end = week.AddDays(6); var slots = normal.Where(TutorAvailabilityPolicy.IsOpen).ToHashSet();
        var list = adjustments.ToList();
        foreach (var item in list.Where(x => x.BlocksOriginal && x.ClassLessonChange.OriginalDate >= week && x.ClassLessonChange.OriginalDate <= end))
            slots.ExceptWith(Range(item.ClassLessonChange.OriginalDate, item.ClassLessonChange.OriginalStartTime, item.ClassLessonChange.OriginalEndTime));
        foreach (var item in list.Where(x => x.AddedDate >= week && x.AddedDate <= end && x.AddedStart.HasValue && x.AddedEnd.HasValue))
            slots.UnionWith(Range(item.AddedDate!.Value, item.AddedStart!.Value, item.AddedEnd!.Value));
        return slots.Order().ToList();
    }
    public async Task<List<int>> WeekAsync(string tutorId, DateTime date)
    {
        var week = TutorAvailabilityPolicy.WeekStart(date);
        return Apply(await new TutorAvailabilityService(db).GetSlotsAsync(tutorId), week, await AdjustmentsAsync(tutorId, week, week.AddDays(6)));
    }
    public async Task<string?> RecurringErrorAsync(string tutorId, DateTime from, DateTime to, DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        var key = (tutorId,from.Date,to.Date);
        if (recurringKey != key)
        {
            recurringNormal = await new TutorAvailabilityService(db).GetSlotsAsync(tutorId);
            recurringChanges = await AdjustmentsAsync(tutorId,TutorAvailabilityPolicy.WeekStart(from),to);
            recurringKey = key;
        }
        var normal = recurringNormal; var changes = recurringChanges;
        for (var date = from.Date.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7); date <= to.Date; date = date.AddDays(7))
            if (date.Add(end.ToTimeSpan()) > ClassScheduleService.Now && LateMonthEnrollmentPolicy.IsRegularTeachingDate(date)
                && !TutorAvailabilityService.Covers(Apply(normal, date, changes), (int)day, start, end))
                return $"The tutor is unavailable on {date:dd MMM yyyy} because of a dated availability adjustment.";
        return null;
    }
    public async Task BlockOriginalAsync(ClassLessonChange change, string actor)
    {
        bool full = await db.TutorProfiles.AnyAsync(x => x.ApplicationUserId == change.ClassSession.TutorUserId && x.EmploymentType == TutorEmploymentType.FullTime);
        if (full) db.TutorAvailabilityAdjustments.Add(new TutorAvailabilityAdjustment {
            ClassLessonChange=change, TutorUserId=change.ClassSession.TutorUserId, BlocksOriginal=true,
            ChangedByUserId=actor, UpdatedAtUtc=DateTime.UtcNow });
    }
    public async Task<string?> ReplacementErrorAsync(ClassLessonChange change, DateTime date, TimeOnly start, TimeOnly end)
    {
        if (date.DayOfWeek == DayOfWeek.Sunday || start < new TimeOnly(8,0) || end > new TimeOnly(22,0))
            return "Replacement must be within Monday–Saturday, 08:00–22:00 MYT.";
        if (!Aligned(start) || !Aligned(end)) return "Choose replacement times in 30-minute increments.";
        var tutorId = change.ClassSession.TutorUserId;
        var profile = await db.TutorProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ApplicationUserId == tutorId && x.IsActive && x.ApplicationUser.IsActive && !x.ApplicationUser.MustChangePassword);
        if (profile == null || !TutorAvailabilityPolicy.IsTypeValid(profile.EmploymentType)) return "The tutor is not available for teaching.";
        var own = await db.TutorAvailabilityAdjustments.AsNoTracking().FirstOrDefaultAsync(x => x.ClassLessonChangeId == change.ClassLessonChangeId);
        if (own != null && own.TutorUserId != tutorId) return "The class tutor has changed. Ask the administrator to review this replacement.";
        bool full = own?.BlocksOriginal ?? profile.EmploymentType == TutorEmploymentType.FullTime;
        var normal = await new TutorAvailabilityService(db).GetSlotsAsync(tutorId);
        if (TutorAvailabilityPolicy.DaysOverLimit(normal).Count > 0 || normal.Count > TutorAvailabilityPolicy.MaxWeeklySlots)
            return "The tutor must correct their regular availability before scheduling replacements.";
        var week = TutorAvailabilityPolicy.WeekStart(date);
        var changes = await AdjustmentsAsync(tutorId, week, week.AddDays(6));
        changes.RemoveAll(x => x.ClassLessonChangeId == change.ClassLessonChangeId);
        var proposal = new TutorAvailabilityAdjustment { ClassLessonChangeId=change.ClassLessonChangeId, ClassLessonChange=change, TutorUserId=tutorId, BlocksOriginal=full };
        changes.Add(proposal);
        var before = Apply(normal, week, changes);
        var target = Range(date,start,end);
        if (full)
        {
            if (week != TutorAvailabilityPolicy.WeekStart(change.OriginalDate)) return "Full-time replacements must stay in the original week.";
            var original = Range(change.OriginalDate,change.OriginalStartTime,change.OriginalEndTime);
            if (!Aligned(change.OriginalStartTime) || !Aligned(change.OriginalEndTime) || target.Count != original.Count)
                return "Full-time replacements must have the same duration as the original lesson.";
            if (!TutorAvailabilityService.Covers(normal,(int)change.OriginalDate.DayOfWeek,change.OriginalStartTime,change.OriginalEndTime))
                return "The original lesson is outside the fixed full-time timetable. Ask the administrator to resolve this legacy lesson.";
            if (target.Any(before.Contains)) return "Choose new hours outside the existing availability so the removed hours can be replaced without double-counting.";
        }
        if (changes.Any(x => x.BlocksOriginal && x.ClassLessonChange.OriginalDate == date.Date
            && start < x.ClassLessonChange.OriginalEndTime && end > x.ClassLessonChange.OriginalStartTime))
            return "The proposed time is a blocked original lesson window. Choose another time.";
        proposal.AddedDate=date.Date; proposal.AddedStart=start; proposal.AddedEnd=end;
        var after = Apply(normal,week,changes);
        if (TutorAvailabilityPolicy.DaysOverLimit(after).Count > 0) return "The proposed availability exceeds eight hours on a day in this week.";
        if (after.Count > TutorAvailabilityPolicy.MaxWeeklySlots) return "The proposed availability exceeds 45 hours in this week.";
        var lessons = (await new ClassScheduleService(db).GetLessonsAsync(week,week.AddDays(6),tutorId))
            .Where(x => !x.Cancelled && x.ChangeId != change.ClassLessonChangeId).ToList();
        double extra = (end-start).TotalHours;
        if (lessons.Where(x => x.Date.Date == date.Date).Sum(x => (x.End-x.Start).TotalHours)+extra > 8)
            return "The tutor's scheduled lessons, including this replacement, would exceed eight hours on this date.";
        if (lessons.Sum(x => (x.End-x.Start).TotalHours)+extra > 45)
            return "The tutor's scheduled lessons, including this replacement, would exceed 45 hours in this week.";
        return null;
    }
    public async Task<string?> PatternChangeErrorAsync(string tutorId, IEnumerable<int> proposed)
    {
        var today=ClassScheduleService.Today;
        var changes=await db.TutorAvailabilityAdjustments.AsNoTracking().Include(x=>x.ClassLessonChange)
            .Where(x=>x.TutorUserId==tutorId && x.AddedDate>=today.AddDays(-6)).ToListAsync();
        foreach(var week in changes.Where(x=>x.AddedDate.HasValue).Select(x=>TutorAvailabilityPolicy.WeekStart(x.AddedDate!.Value)).Distinct())
        {
            if(week.AddDays(6)<today)continue;
            var slots=Apply(proposed,week,changes);
            if(slots.Count>TutorAvailabilityPolicy.MaxWeeklySlots || TutorAvailabilityPolicy.DaysOverLimit(slots).Count>0)
                return $"This change would exceed the daily or weekly limit in the week of {week:dd MMM yyyy}, including existing replacements.";
        }
        return null;
    }
    public async Task SaveReplacementAsync(ClassLessonChange change, DateTime date, TimeOnly start, TimeOnly end, string actor)
    {
        var own = await db.TutorAvailabilityAdjustments.FirstOrDefaultAsync(x => x.ClassLessonChangeId == change.ClassLessonChangeId);
        if (own == null)
        {
            bool full = await db.TutorProfiles.AnyAsync(x => x.ApplicationUserId == change.ClassSession.TutorUserId && x.EmploymentType == TutorEmploymentType.FullTime);
            own = new TutorAvailabilityAdjustment { ClassLessonChange=change, TutorUserId=change.ClassSession.TutorUserId, BlocksOriginal=full };
            db.TutorAvailabilityAdjustments.Add(own);
        }
        own.AddedDate=date.Date; own.AddedStart=start; own.AddedEnd=end; own.ChangedByUserId=actor; own.UpdatedAtUtc=DateTime.UtcNow;
    }
    public async Task ClearReplacementAsync(ClassLessonChange change, string actor)
    {
        var own = await db.TutorAvailabilityAdjustments.FirstOrDefaultAsync(x => x.ClassLessonChangeId == change.ClassLessonChangeId);
        if (own == null) return;
        own.AddedDate=null; own.AddedStart=null; own.AddedEnd=null; own.ChangedByUserId=actor; own.UpdatedAtUtc=DateTime.UtcNow;
    }
}
