using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Attendance;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.Services.Attendance;
public sealed class AttendanceService(ApplicationDbContext db)
{
    public static bool Editable(DateTime date, TimeOnly start, TimeOnly end, DateTime? submitted, DateTime now) =>
        submitted == null && start < end && now >= date.Date.Add(start.ToTimeSpan()) && now < date.Date.Add(end.ToTimeSpan());
    public static string State(DateTime date, TimeOnly start, TimeOnly end, DateTime? submitted, bool saved, DateTime now) =>
        submitted != null ? "Submitted" : now >= date.Date.Add(end.ToTimeSpan()) ? (saved ? "Locked" : "Unrecorded (locked)")
        : now < date.Date.Add(start.ToTimeSpan()) ? "Upcoming" : saved ? "Draft" : "Unrecorded";
    private IQueryable<AttendanceLesson> Saved(string userId, bool admin) => db.Set<AttendanceLesson>().AsNoTracking()
        .Where(x => admin || x.TutorUserId == userId);
    private async Task<LessonItem?> CurrentAsync(int classId, DateTime date, int? changeId, bool replacement, string userId, bool admin)
    {
        var lessons = await new ClassScheduleService(db).GetLessonsAsync(date.Date, date.Date, admin ? null : userId, classId);
        return lessons.FirstOrDefault(x => !x.Cancelled && x.Class.Status == ClassSessionStatus.Active
            && x.Replacement == replacement && x.ChangeId == changeId);
    }
    private static bool Same(AttendanceLesson saved, LessonItem current) =>
        saved.ClassSessionId == current.Class.ClassSessionId && saved.OriginalDate == current.OriginalDate.Date
        && saved.LessonDate == current.Date.Date && saved.IsReplacement == current.Replacement
        && saved.ClassLessonChangeId == current.ChangeId && saved.StartTime == current.Start
        && saved.EndTime == current.End && saved.TutorUserId == current.Class.TutorUserId;
    private async Task<List<int>> RosterAsync(LessonItem lesson)
    {
        if (!lesson.Replacement) return await new ClassScheduleService(db).RosterAsync(lesson.Class.ClassSessionId, lesson.Date.Date);
        var json = await db.ClassLessonChanges.AsNoTracking().Where(x => x.ClassLessonChangeId == lesson.ChangeId)
            .Select(x => x.StudentIdsJson).SingleAsync();
        return (JsonSerializer.Deserialize<List<int>>(json) ?? []).Distinct().ToList();
    }
    public async Task<AttendanceIndexViewModel> IndexAsync(DateTime date, string userId, bool admin)
    {
        var monday = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var model = new AttendanceIndexViewModel { WeekStart = monday, IsAdmin = admin };
        var saved = await Saved(userId, admin).Where(x => x.LessonDate >= monday && x.LessonDate <= model.WeekEnd).ToListAsync();
        var lessons = await new ClassScheduleService(db).GetLessonsAsync(monday, model.WeekEnd, admin ? null : userId);
        var now = ClassScheduleService.Now;
        foreach (var lesson in lessons.Where(x => !x.Cancelled && x.Class.Status == ClassSessionStatus.Active))
        {
            if (saved.Any(x => x.ClassSessionId == lesson.Class.ClassSessionId && x.OriginalDate == lesson.OriginalDate.Date && x.IsReplacement == lesson.Replacement)) continue;
            model.Lessons.Add(new AttendanceLessonRow
            {
                ClassSessionId = lesson.Class.ClassSessionId,
                Date = lesson.Date,
                ChangeId = lesson.ChangeId,
                Replacement = lesson.Replacement,
                ClassCode = lesson.Class.ClassCode,
                ClassName = lesson.Class.ClassName ?? "",
                TutorName = lesson.Class.TutorUser.FullName ?? "",
                Start = lesson.Start,
                End = lesson.End,
                State = State(lesson.Date, lesson.Start, lesson.End, null, false, now)
            });
        }
        foreach (var record in saved)
            model.Lessons.Add(new AttendanceLessonRow
            {
                SavedId = record.AttendanceLessonId,
                ClassSessionId = record.ClassSessionId,
                Date = record.LessonDate,
                ChangeId = record.ClassLessonChangeId,
                Replacement = record.IsReplacement,
                ClassCode = record.ClassCode,
                ClassName = record.ClassName,
                TutorName = record.TutorName,
                Start = record.StartTime,
                End = record.EndTime,
                State = State(record.LessonDate, record.StartTime, record.EndTime, record.SubmittedAtUtc, true, now)
            });
        model.Lessons = model.Lessons.OrderBy(x => x.Date).ThenBy(x => x.Start).ThenBy(x => x.ClassCode).ToList();
        return model;
    }
    public async Task<AttendanceSheetViewModel?> SheetAsync(int classId, DateTime date, int? changeId, bool replacement, int? savedId, string userId, bool admin)
    {
        AttendanceLesson? saved;
        if (savedId.HasValue)
            saved = await Saved(userId, admin).Include(x => x.Records).SingleOrDefaultAsync(x => x.AttendanceLessonId == savedId);
        else
            saved = await Saved(userId, admin).Include(x => x.Records).SingleOrDefaultAsync(x => x.ClassSessionId == classId
                && x.LessonDate == date.Date && x.ClassLessonChangeId == changeId && x.IsReplacement == replacement);
        if (savedId.HasValue && saved == null) return null;
        if (saved != null) { classId = saved.ClassSessionId; date = saved.LessonDate; changeId = saved.ClassLessonChangeId; replacement = saved.IsReplacement; }
        var lesson = await CurrentAsync(classId, date, changeId, replacement, userId, admin);
        if (saved == null && lesson == null) return null;
        var model = new AttendanceSheetViewModel
        {
            ClassSessionId = classId,
            Date = date.Date,
            ChangeId = changeId,
            Replacement = replacement,
            ClassCode = saved?.ClassCode ?? lesson!.Class.ClassCode,
            ClassName = saved?.ClassName ?? lesson!.Class.ClassName ?? "",
            TutorName = saved?.TutorName ?? lesson!.Class.TutorUser.FullName ?? "",
            Start = saved?.StartTime ?? lesson!.Start,
            End = saved?.EndTime ?? lesson!.End,
            RowVersion = saved == null ? "" : Convert.ToBase64String(saved.RowVersion)
        };
        model.State = State(date, model.Start, model.End, saved?.SubmittedAtUtc, saved != null, ClassScheduleService.Now);
        model.CanEdit = !admin && lesson != null && (saved == null || Same(saved, lesson))
            && Editable(date, model.Start, model.End, saved?.SubmittedAtUtc, ClassScheduleService.Now);
        if (saved != null && (lesson == null || !Same(saved, lesson))) model.State += " · Schedule changed (read only)";
        if (saved != null)
            model.Students = saved.Records.OrderBy(x => x.StudentNumber).Select(x => new AttendanceStudentRow
            {
                StudentProfileId = x.StudentProfileId,
                StudentNumber = x.StudentNumber,
                StudentName = x.StudentName,
                Status = x.Status,
                Remark = x.Remark
            }).ToList();
        else
        {
            var ids = await RosterAsync(lesson!);
            model.Students = await db.StudentProfiles.AsNoTracking().Where(x => ids.Contains(x.StudentProfileId)).OrderBy(x => x.StudentNumber)
                .Select(x => new AttendanceStudentRow { StudentProfileId = x.StudentProfileId, StudentNumber = x.StudentNumber, StudentName = x.FullName }).ToListAsync();
        }
        return model;
    }
    public async Task<string?> SaveAsync(SaveAttendanceViewModel input, string userId)
    {
        if (input.Students == null || input.Students.Count == 0 || input.Students.Count > 100
            || input.Students.Select(x => x.StudentProfileId).Distinct().Count() != input.Students.Count
            || input.Students.Any(x => (x.Status.HasValue && !Enum.IsDefined(x.Status.Value)) || x.Remark?.Length > 300))
            return "Please enter valid attendance records.";
        if (input.FinalSubmit && input.Students.Any(x => x.Status == null)) return "Select Present, Absent or Late for every student before submitting.";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var lesson = await CurrentAsync(input.ClassSessionId, input.Date, input.ChangeId, input.Replacement, userId, false);
            if (lesson == null) return "This lesson is no longer available. Return to Attendance and reload.";
            if (!await db.TutorProfiles.AnyAsync(x => x.ApplicationUserId == userId && x.IsActive && x.ApplicationUser.IsActive && !x.ApplicationUser.MustChangePassword))
                return "Your tutor account is not available for recording attendance.";
            var saved = await db.Set<AttendanceLesson>().Include(x => x.Records).SingleOrDefaultAsync(x =>
                x.ClassSessionId == lesson.Class.ClassSessionId && x.OriginalDate == lesson.OriginalDate.Date && x.IsReplacement == lesson.Replacement);
            if (saved != null && (!Same(saved, lesson) || saved.TutorUserId != userId)) return "The schedule has changed. Existing attendance is read only.";
            if (!Editable(lesson.Date, lesson.Start, lesson.End, saved?.SubmittedAtUtc, ClassScheduleService.Now))
                return "Attendance can only be edited during the lesson, before final submission.";
            string version = saved == null ? "" : Convert.ToBase64String(saved.RowVersion);
            if (!string.Equals(input.RowVersion ?? "", version, StringComparison.Ordinal)) return "Attendance changed in another tab. Reload before saving again.";
            var ids = saved == null ? await RosterAsync(lesson) : saved.Records.Select(x => x.StudentProfileId).ToList();
            if (!ids.ToHashSet().SetEquals(input.Students.Select(x => x.StudentProfileId)))
                return "The student roster has changed. Reload this page before saving.";
            if (saved == null)
            {
                var students = await db.StudentProfiles.Where(x => ids.Contains(x.StudentProfileId)).ToListAsync();
                if (students.Count != ids.Count) return "The student roster is no longer valid. Reload the page.";
                saved = new AttendanceLesson
                {
                    ClassSessionId = lesson.Class.ClassSessionId,
                    OriginalDate = lesson.OriginalDate.Date,
                    LessonDate = lesson.Date.Date,
                    IsReplacement = lesson.Replacement,
                    ClassLessonChangeId = lesson.ChangeId,
                    TutorUserId = userId,
                    ClassCode = lesson.Class.ClassCode,
                    ClassName = lesson.Class.ClassName ?? "",
                    TutorName = lesson.Class.TutorUser.FullName ?? "",
                    StartTime = lesson.Start,
                    EndTime = lesson.End,
                    Records = students.Select(x => new AttendanceRecord { StudentProfileId = x.StudentProfileId, StudentNumber = x.StudentNumber, StudentName = x.FullName }).ToList()
                };
                db.Set<AttendanceLesson>().Add(saved);
            }
            var posted = input.Students.ToDictionary(x => x.StudentProfileId);
            foreach (var record in saved.Records) { record.Status = posted[record.StudentProfileId].Status; record.Remark = posted[record.StudentProfileId].Remark?.Trim(); }
            saved.UpdatedAtUtc = DateTime.UtcNow; saved.UpdatedByUserId = userId;
            if (input.FinalSubmit) saved.SubmittedAtUtc = saved.UpdatedAtUtc;
            if (!Editable(lesson.Date, lesson.Start, lesson.End, null, ClassScheduleService.Now)) return "This lesson has ended. Attendance is now locked.";
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (SqlException ex) when (ex.Number == 1205)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Another request changed this lesson. Reload before saving again."; }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Attendance changed in another tab. Reload before saving again."; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627 || sql.Number == 1205))
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Another request changed this lesson. Reload before saving again."; }
    }
}