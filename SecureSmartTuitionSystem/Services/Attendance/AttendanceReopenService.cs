using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.Services.Attendance;
public sealed class AttendanceReopenService(ApplicationDbContext db)
{
    private async Task<LessonItem?> Lesson(int classId, DateTime date, int? changeId, bool replacement, string tutorId) =>
        (await new ClassScheduleService(db).GetLessonsAsync(date.Date, date.Date, tutorId, classId))
        .SingleOrDefault(x => !x.Cancelled && x.Class.Status == ClassSessionStatus.Active && x.ChangeId == changeId && x.Replacement == replacement);
    public static bool Same(AttendanceReopenRequest request, LessonItem lesson) => request.TutorUserId == lesson.Class.TutorUserId &&
        request.ClassSessionId == lesson.Class.ClassSessionId && request.OriginalDate == lesson.OriginalDate.Date &&
        request.LessonDate == lesson.Date.Date && request.StartTime == lesson.Start && request.EndTime == lesson.End &&
        request.ChangeId == lesson.ChangeId && request.IsReplacement == lesson.Replacement;
    public async Task<DateTime?> GrantUntilAsync(LessonItem lesson) => await db.Set<AttendanceReopenRequest>().AsNoTracking()
        .Where(r => r.Decision == AttendanceReopenDecision.Approved && r.TutorUserId == lesson.Class.TutorUserId &&
            r.ClassSessionId == lesson.Class.ClassSessionId && r.OriginalDate == lesson.OriginalDate.Date && r.LessonDate == lesson.Date.Date &&
            r.StartTime == lesson.Start && r.EndTime == lesson.End && r.ChangeId == lesson.ChangeId && r.IsReplacement == lesson.Replacement)
        .Select(r => r.OpenUntilUtc).MaxAsync();
    private Task<bool> Submitted(LessonItem lesson) => db.Set<AttendanceLesson>().AnyAsync(x => x.ClassSessionId == lesson.Class.ClassSessionId &&
        x.OriginalDate == lesson.OriginalDate.Date && x.IsReplacement == lesson.Replacement && x.SubmittedAtUtc != null);
    private async Task<bool> Compatible(LessonItem lesson)
    {
        var saved = await db.Set<AttendanceLesson>().AsNoTracking().SingleOrDefaultAsync(x => x.ClassSessionId == lesson.Class.ClassSessionId && x.OriginalDate == lesson.OriginalDate.Date && x.IsReplacement == lesson.Replacement);
        return saved == null || saved.TutorUserId == lesson.Class.TutorUserId && saved.LessonDate == lesson.Date.Date && saved.StartTime == lesson.Start && saved.EndTime == lesson.End && saved.ClassLessonChangeId == lesson.ChangeId;
    }
    private Task<bool> ActiveTutor(string id) => db.TutorProfiles.AnyAsync(t => t.ApplicationUserId == id && t.IsActive && t.ApplicationUser.IsActive && !t.ApplicationUser.MustChangePassword);
    public async Task<string?> RequestAsync(int classId, DateTime date, int? changeId, bool replacement, string? reason, string tutorId)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500) return "Enter a reopening reason of 1–500 characters.";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var lesson = await Lesson(classId,date,changeId,replacement,tutorId);
        if (lesson == null || !await ActiveTutor(tutorId)) return "This lesson is no longer available to you.";
        if (!await Compatible(lesson)) return "Existing attendance belongs to an earlier schedule and cannot be reopened. Contact the administrator.";
        if (await Submitted(lesson)) return "Attendance was already submitted. Use Request Correction instead.";
        var grant = await GrantUntilAsync(lesson);
        if (ClassScheduleService.Now < AttendanceEntryPolicy.Deadline(lesson.Date,lesson.End,grant)) return "The entry window is still open. Record attendance directly.";
        if (await db.Set<AttendanceReopenRequest>().AnyAsync(r => r.ClassSessionId == classId && r.OriginalDate == lesson.OriginalDate.Date && r.IsReplacement == replacement && r.Decision == AttendanceReopenDecision.Pending))
            return "This lesson already has a pending reopening request.";
        db.Set<AttendanceReopenRequest>().Add(new() {
            ClassSessionId=classId, OriginalDate=lesson.OriginalDate.Date, LessonDate=lesson.Date.Date, StartTime=lesson.Start, EndTime=lesson.End,
            ChangeId=changeId, IsReplacement=replacement, TutorUserId=tutorId, TutorName=lesson.Class.TutorUser.FullName ?? "",
            ClassName=lesson.Class.ClassName ?? lesson.Class.ClassCode, Reason=reason, RequestedAtUtc=DateTime.UtcNow });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
    }
    public Task<List<AttendanceReopenRequest>> ListAsync(string userId,bool admin) => db.Set<AttendanceReopenRequest>().AsNoTracking()
        .Where(x=>admin || x.TutorUserId==userId).OrderByDescending(x=>x.RequestedAtUtc).Take(500).ToListAsync();
    public async Task<string?> ReviewAsync(int id,bool approve,string? remark,string? version,string adminId)
    {
        remark=remark?.Trim();
        if(string.IsNullOrEmpty(remark)||remark.Length>500) return "Enter a review reason of 1–500 characters.";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(!await db.Users.AnyAsync(u=>u.Id==adminId && u.IsActive && db.UserRoles.Any(ur=>ur.UserId==u.Id && db.Roles.Any(r=>r.Id==ur.RoleId && r.NormalizedName=="ADMIN")))) return "Your account cannot review this request.";
        var request=await db.Set<AttendanceReopenRequest>().SingleOrDefaultAsync(r=>r.AttendanceReopenRequestId==id);
        if(request==null||request.Decision!=AttendanceReopenDecision.Pending) return "This request is no longer pending.";
        if(Convert.ToBase64String(request.RowVersion)!=version) return "This request changed. Reload before reviewing.";
        if(approve) {
            var lesson=await Lesson(request.ClassSessionId,request.LessonDate,request.ChangeId,request.IsReplacement,request.TutorUserId);
            if(lesson==null||!Same(request,lesson)||!await ActiveTutor(request.TutorUserId)||!await Compatible(lesson)) return "The lesson or assigned tutor changed. Reject this request and ask for a new one.";
            if(await Submitted(lesson)) return "Attendance is already submitted. Reject this request; corrections use the existing correction workflow.";
            request.OpenUntilUtc=DateTime.UtcNow.AddHours(24);
        }
        request.Decision=approve?AttendanceReopenDecision.Approved:AttendanceReopenDecision.Rejected;
        request.ReviewedByUserId=adminId;request.ReviewedAtUtc=DateTime.UtcNow;request.ReviewRemark=remark;
        try { await db.SaveChangesAsync();await tx.CommitAsync();return null; }
        catch(DbUpdateConcurrencyException){await tx.RollbackAsync();db.ChangeTracker.Clear();return "This request changed. Reload before reviewing.";}
    }
}
