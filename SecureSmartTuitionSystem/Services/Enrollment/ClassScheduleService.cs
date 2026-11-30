using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class ClassScheduleService(ApplicationDbContext db)
{
    public static DateTime Today => DateTime.UtcNow.AddHours(8).Date;
    public static DateTime Now => DateTime.UtcNow.AddHours(8);
    public static DateTime FirstDate(ClassSession c) => c.StartDate?.Date ?? c.CreatedAt.AddHours(8).Date;
    public static bool WeeklyOverlap(DateTime startA, DateTime? endA, DayOfWeek dayA, TimeOnly timeA, TimeOnly endTimeA,
        DateTime startB, DateTime? endB, DayOfWeek dayB, TimeOnly timeB, TimeOnly endTimeB)
    {
        if (dayA != dayB || timeA >= endTimeB || endTimeA <= timeB) return false;
        var from = startA.Date > startB.Date ? startA.Date : startB.Date;
        var to = endA.HasValue && endB.HasValue ? (endA.Value.Date < endB.Value.Date ? endA.Value.Date : endB.Value.Date) : endA?.Date ?? endB?.Date ?? new DateTime(2100, 12, 31);
        for (var date = from.AddDays(((int)dayA - (int)from.DayOfWeek + 7) % 7); date <= to; date = date.AddDays(7))
            if (LateMonthEnrollmentPolicy.IsRegularTeachingDate(date)) return true;
        return false;
    }
    public static bool ValidOn(ClassSession c, DateTime date) => c.Status == ClassSessionStatus.Active && date.Date >= FirstDate(c) && date.Date <= AcademicYearPolicy.ClassEnd(c);
    public async Task<List<LessonItem>> GetLessonsAsync(DateTime from, DateTime to, string? tutorId = null, int? classId = null)
    {
        var query = db.ClassSessions.AsNoTracking().Include(x => x.Subject).Include(x => x.TutorUser).AsQueryable();
        if (tutorId != null) query = query.Where(x => x.TutorUserId == tutorId);
        if (classId.HasValue) query = query.Where(x => x.ClassSessionId == classId.Value);
        var classes = await query.ToListAsync();
        var ids = classes.Select(x => x.ClassSessionId).ToList();
        var changes = await db.ClassLessonChanges.AsNoTracking().Where(x => ids.Contains(x.ClassSessionId) &&
            ((x.OriginalDate >= from && x.OriginalDate <= to) || (x.ReplacementDate >= from && x.ReplacementDate <= to))).ToListAsync();
        return BuildLessons(classes, changes, from, to);
    }
    public static List<LessonItem> BuildLessons(List<ClassSession> classes, List<ClassLessonChange> changes, DateTime from, DateTime to)
    {
        var result = new List<LessonItem>();
        foreach (var c in classes)
        {
            for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            {
                var change = changes.FirstOrDefault(x => x.ClassSessionId == c.ClassSessionId && x.OriginalDate.Date == date);
                // Archiving must retain past regular lessons, without reopening enrollment eligibility.
                var closedHistory = c.Status == ClassSessionStatus.Closed && c.ClosedAtUtc.HasValue &&
                    date >= FirstDate(c) && date <= AcademicYearPolicy.ClassEnd(c) &&
                    date.Add(c.EndTime.ToTimeSpan()) <= c.ClosedAtUtc.Value.AddHours(8);
                if (change == null && ((!ValidOn(c, date) && !closedHistory) || !LateMonthEnrollmentPolicy.IsRegularTeachingDate(date) || date.DayOfWeek != c.DayOfWeek)) continue;
                result.Add(new LessonItem { Class = c, Date = date, OriginalDate = date,
                    Start = change?.OriginalStartTime ?? c.StartTime, End = change?.OriginalEndTime ?? c.EndTime,
                    Venue = c.Venue, Cancelled = change != null, ChangeId = change?.ClassLessonChangeId, Reason = change?.CancellationReason ?? "" });
            }
            foreach (var change in changes.Where(x => x.ClassSessionId == c.ClassSessionId && x.ReplacementDate.HasValue && x.ReplacementDate >= from && x.ReplacementDate <= to))
                result.Add(new LessonItem { Class = c, Date = change.ReplacementDate!.Value.Date,
                    Start = change.ReplacementStartTime!.Value, End = change.ReplacementEndTime!.Value,
                    Venue = change.ReplacementVenue ?? c.Venue, Replacement = true,
                    ChangeId = change.ClassLessonChangeId, OriginalDate = change.OriginalDate });
        }
        return result.OrderBy(x => x.Date).ThenBy(x => x.Start).ThenBy(x => x.Class.ClassCode).ToList();
    }
    public async Task<List<int>> RosterAsync(int classId, DateTime date) => await db.StudentEnrollments.AsNoTracking()
        .Where(x => x.ClassSessionId == classId && x.StartDate <= date && x.StartDate.Year == date.Year &&
            (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop || x.Status == StudentEnrollmentStatus.Dropped || x.Status == StudentEnrollmentStatus.Completed) &&
            (!x.DropEffectiveDate.HasValue || x.DropEffectiveDate.Value > date) &&
            (!x.EndedAt.HasValue || x.EndedAt.Value > date))
        .Select(x => x.StudentProfileId).Distinct().ToListAsync();
    public async Task<List<string>> RecipientsAsync(List<int> students, string tutorId)
    {
        var users = await db.StudentProfiles.Where(x => students.Contains(x.StudentProfileId) && x.ApplicationUserId != null)
            .Select(x => x.ApplicationUserId!).ToListAsync();
        users.AddRange(await db.ParentStudents.Where(x => students.Contains(x.StudentProfileId) && x.IsActive)
            .Select(x => x.ParentProfile.ApplicationUserId).ToListAsync());
        users.Add(tutorId);
        return users.Distinct().ToList();
    }
    public async Task NotifyAsync(IEnumerable<string> users, string title, string text, int classId)
    {
        var enrollments = await db.StudentEnrollments.AsNoTracking().Where(x => x.ClassSessionId == classId)
            .Select(x => new { x.StudentProfileId, x.StartDate, x.StudentProfile.ApplicationUserId }).ToListAsync();
        var studentIds = enrollments.Select(x => x.StudentProfileId).Distinct().ToList();
        var parents = await db.ParentStudents.AsNoTracking().Where(x => studentIds.Contains(x.StudentProfileId) && x.IsActive)
            .Select(x => new { x.StudentProfileId, x.ParentProfile.ApplicationUserId }).ToListAsync();
        foreach (var user in users.Distinct())
        {
            var dates = enrollments.Where(x => x.ApplicationUserId == user || parents.Any(p => p.ApplicationUserId == user && p.StudentProfileId == x.StudentProfileId))
                .Select(x => (DateTime?)x.StartDate.Date);
            db.UserNotifications.Add(new UserNotification { UserId = user, Title = title, Text = text, AvailableFromDate = dates.Min() });
        }
    }
    public async Task<bool> HasConflictAsync(ClassLessonChange change, DateTime date, TimeOnly start, TimeOnly end)
    {
        var lessons = (await GetLessonsAsync(date, date)).Where(x => !x.Cancelled && x.ChangeId != change.ClassLessonChangeId && start < x.End && end > x.Start).ToList();
        if (lessons.Any(x => x.Class.TutorUserId == change.ClassSession.TutorUserId)) return true;
        var originalStudents = JsonSerializer.Deserialize<List<int>>(change.StudentIdsJson) ?? new();
        foreach (var lesson in lessons)
        {
            List<int> roster;
            if (lesson.Replacement)
            {
                var other = await db.ClassLessonChanges.AsNoTracking().FirstAsync(x => x.ClassLessonChangeId == lesson.ChangeId);
                roster = JsonSerializer.Deserialize<List<int>>(other.StudentIdsJson) ?? new();
            }
            else roster = await RosterAsync(lesson.Class.ClassSessionId, date);
            if (roster.Intersect(originalStudents).Any()) return true;
        }
        return false;
    }
    public static bool MeetLink(string? link) => Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        uri.Host.Equals("meet.google.com", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')) && string.IsNullOrEmpty(uri.UserInfo);
}
