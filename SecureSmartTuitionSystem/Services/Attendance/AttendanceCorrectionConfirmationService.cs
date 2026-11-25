using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.ViewModels.Attendance;
namespace SecureSmartTuitionSystem.Services.Attendance;
public sealed partial class AttendanceCorrectionService
{
    public IQueryable<AttendanceCorrectionRequest> VisibleRequests(string userId, bool admin, bool tutor) =>
        db.Set<AttendanceCorrectionRequest>().AsNoTracking().Where(r => admin || r.RequestedByUserId == userId ||
            (tutor && r.TutorConfirmationRequestedAtUtc != null && (r.ConfirmationTutorUserId == userId || r.TutorConfirmedByUserId == userId)));

    private Task<bool> ActiveRole(string id, string role) => db.Users.AnyAsync(u => u.Id == id && u.IsActive && !u.MustChangePassword &&
        db.UserRoles.Any(ur => ur.UserId == id && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == role)) &&
        (role != "TUTOR" || !db.UserRoles.Any(ur => ur.UserId == id && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN"))));

    public async Task<AttendanceCorrectionListViewModel> ListAsync(string userId, bool admin, string status, string sort = "requested_asc", bool tutor = false, string? search = null, DateTime? from = null, DateTime? to = null, int? requestId = null)
    {
        var q = VisibleRequests(userId, admin, tutor);
        if (requestId.HasValue) q = q.Where(r => r.AttendanceCorrectionRequestId == requestId.Value);
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) q = q.Where(r => r.AttendanceRecord.StudentNumber.Contains(search) || r.AttendanceRecord.StudentName.Contains(search) || r.AttendanceRecord.AttendanceLesson.ClassCode.Contains(search));
        if (from.HasValue) { var utc = from.Value.Date.AddHours(-8); q = q.Where(r => r.RequestedAtUtc >= utc); }
        if (to.HasValue) { var utc = to.Value.Date.AddDays(1).AddHours(-8); q = q.Where(r => r.RequestedAtUtc < utc); }
        if (status == "confirmation") q = q.Where(r => r.Decision == CorrectionDecision.Pending && r.TutorConfirmationRequestedAtUtc != null && r.TutorConfirmedAtUtc == null);
        else if (status == "review") q = q.Where(r => r.Decision == CorrectionDecision.Pending && (r.TutorConfirmationRequestedAtUtc == null || (r.TutorConfirmedAtUtc != null && r.TutorSupportsCorrection != null)));
        else if (status != "all") {
            var decision = status == "approved" ? CorrectionDecision.Approved : status == "rejected" ? CorrectionDecision.Rejected : CorrectionDecision.Pending;
            q = q.Where(r => r.Decision == decision);
        }
        var rows = await Order(q, sort).Include(r => r.AttendanceRecord).ThenInclude(r => r.AttendanceLesson).Take(500).ToListAsync();
        var tutors = admin ? await db.Users.Where(u => u.IsActive && !u.MustChangePassword &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "TUTOR")) &&
            !db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")))
            .OrderBy(u => u.UserName).Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = (u.UserName ?? "") + " · " + (u.FullName ?? "") }).ToListAsync() : [];
        var ids = rows.Select(r => r.ConfirmationTutorUserId ?? r.AttendanceRecord.AttendanceLesson.TutorUserId).Distinct().ToList();
        var tutorNames = await db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.UserName, u.FullName }).ToDictionaryAsync(u => u.Id);
        return new AttendanceCorrectionListViewModel {
            IsAdmin = admin, IsTutor = tutor, Search = search ?? "", Status = status, Sort = sort, From = from, To = to, TutorChoices = tutors,
            Rows = rows.Select(r => {
                var assigned = r.ConfirmationTutorUserId ?? r.AttendanceRecord.AttendanceLesson.TutorUserId;
                tutorNames.TryGetValue(assigned, out var tutorInfo);
                return new AttendanceCorrectionListRow {
                    Id = r.AttendanceCorrectionRequestId, RecordId = r.AttendanceRecordId, Date = r.AttendanceRecord.AttendanceLesson.LessonDate,
                    ClassCode = r.AttendanceRecord.AttendanceLesson.ClassCode, LessonStart = r.AttendanceRecord.AttendanceLesson.StartTime, LessonEnd = r.AttendanceRecord.AttendanceLesson.EndTime, Student = r.AttendanceRecord.StudentNumber + " · " + r.AttendanceRecord.StudentName,
                    RequestedBy = r.RequestedByName, RequestedAtUtc = r.RequestedAtUtc, Reason = r.Reason, PreviousStatus = r.PreviousStatus,
                    CurrentStatus = r.AttendanceRecord.Status, RequestedStatus = r.RequestedStatus, Decision = r.Decision,
                    ReviewedBy = r.ReviewedByName, ReviewedAtUtc = r.ReviewedAtUtc, ReviewRemark = r.ReviewRemark, RowVersion = Convert.ToBase64String(r.RowVersion),
                    IsStudentRequest = r.IsStudentRequest, CanReview = admin && r.Decision == CorrectionDecision.Pending,
                    ApprovalRemarksOptional = !AttendanceConfirmationRules.ReviewRemarkRequired(r, "approve"),
                    CanApprove = admin && AttendanceConfirmationRules.CanApprove(r) && r.AttendanceRecord.Status == r.PreviousStatus,
                    CanRequestConfirmation = admin && r.IsStudentRequest && r.Decision == CorrectionDecision.Pending && r.TutorConfirmationRequestedAtUtc == null && r.AttendanceRecord.Status == r.PreviousStatus,
                    CanConfirm = tutor && AttendanceConfirmationRules.WaitingForTutor(r) && r.ConfirmationTutorUserId == userId && r.RequestedByUserId != userId,
                    CanReassign = admin && AttendanceConfirmationRules.WaitingForTutor(r) && !tutors.Any(t => t.Value == assigned),
                    TutorConfirmationRequestedAtUtc = r.TutorConfirmationRequestedAtUtc, ConfirmationRequestedByName = r.ConfirmationRequestedByName,
                    ConfirmationTutorName = tutorInfo == null ? "Unavailable tutor account" : (tutorInfo.UserName ?? "") + " · " + (tutorInfo.FullName ?? ""),
                    TutorSupportsCorrection = r.TutorSupportsCorrection, TutorConfirmationRemark = r.TutorConfirmationRemark,
                    TutorConfirmedAtUtc = r.TutorConfirmedAtUtc, TutorConfirmedByName = r.TutorConfirmedByName, StatusLabel = AttendanceConfirmationRules.Label(r)
                };
            }).ToList()
        };
    }

    public async Task<string?> RequestTutorConfirmationAsync(int id, string? version, string adminId, string adminName, string? fallbackTutorId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try {
            if (!await ActiveRole(adminId, "ADMIN")) return "Your account cannot request Tutor confirmation.";
            var r = await db.Set<AttendanceCorrectionRequest>().Include(x => x.AttendanceRecord).ThenInclude(x => x.AttendanceLesson).SingleOrDefaultAsync(x => x.AttendanceCorrectionRequestId == id);
            if (r == null || !r.IsStudentRequest || r.Decision != CorrectionDecision.Pending || r.TutorConfirmationRequestedAtUtc != null) return "This request is not available for Tutor confirmation.";
            if (Convert.ToBase64String(r.RowVersion) != version) return "This request changed. Reload before continuing.";
            if (r.AttendanceRecord.Status != r.PreviousStatus) return "Attendance changed. Reject this stale request and ask for a new one.";
            var tutorId = r.AttendanceRecord.AttendanceLesson.TutorUserId;
            if (!await ActiveRole(tutorId, "TUTOR")) {
                if (string.IsNullOrWhiteSpace(fallbackTutorId) || !await ActiveRole(fallbackTutorId, "TUTOR")) return "The lesson tutor is unavailable. Select an active replacement tutor.";
                tutorId = fallbackTutorId;
            }
            if (tutorId == r.RequestedByUserId) return "The applicant cannot confirm their own request.";
            r.TutorConfirmationRequestedAtUtc = DateTime.UtcNow;
            r.ConfirmationRequestedByUserId = adminId; r.ConfirmationRequestedByName = adminName[..Math.Min(adminName.Length,256)];
            r.ConfirmationTutorUserId = tutorId;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (DbUpdateConcurrencyException) { return "This request changed. Reload before continuing."; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 1205) { return "Another reviewer changed this request. Reload."; }
        catch (SqlException ex) when (ex.Number == 1205) { return "Another reviewer changed this request. Reload."; }
    }

    public async Task<string?> ConfirmAsync(int id, bool? supports, string? remark, string? version, string tutorId, string tutorName)
    {
        remark = remark?.Trim();
        if (!supports.HasValue || string.IsNullOrEmpty(remark) || remark.Length > 500) return "Choose an outcome and enter confirmation remarks of 1–500 characters.";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try {
            if (!await ActiveRole(tutorId, "TUTOR")) return "Your account cannot confirm requests.";
            var r = await db.Set<AttendanceCorrectionRequest>().SingleOrDefaultAsync(x => x.AttendanceCorrectionRequestId == id);
            if (r == null || !AttendanceConfirmationRules.WaitingForTutor(r) || r.ConfirmationTutorUserId != tutorId || r.RequestedByUserId == tutorId) return "This request is not waiting for your confirmation.";
            if (Convert.ToBase64String(r.RowVersion) != version) return "This request changed. Reload before confirming.";
            // Tutor may explain a stale record too; only Admin can approve/reject the correction.
            r.TutorSupportsCorrection = supports; r.TutorConfirmationRemark = remark; r.TutorConfirmedAtUtc = DateTime.UtcNow;
            r.TutorConfirmedByUserId = tutorId; r.TutorConfirmedByName = tutorName[..Math.Min(tutorName.Length,256)];
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (DbUpdateConcurrencyException) { return "This request changed. Reload before confirming."; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 1205) { return "Another reviewer changed this request. Reload."; }
        catch (SqlException ex) when (ex.Number == 1205) { return "Another reviewer changed this request. Reload."; }
    }

    public async Task<string?> ReassignConfirmationAsync(int id, string? tutorId, string? version, string adminId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try {
            if (!await ActiveRole(adminId,"ADMIN") || string.IsNullOrWhiteSpace(tutorId) || !await ActiveRole(tutorId,"TUTOR")) return "Choose an active Tutor account.";
            var r = await db.Set<AttendanceCorrectionRequest>().SingleOrDefaultAsync(x => x.AttendanceCorrectionRequestId == id);
            if (r == null || !AttendanceConfirmationRules.WaitingForTutor(r) || Convert.ToBase64String(r.RowVersion) != version) return "This request changed or cannot be reassigned.";
            if (r.ConfirmationTutorUserId != null && await ActiveRole(r.ConfirmationTutorUserId,"TUTOR")) return "The assigned tutor is still active. Ask that tutor to confirm.";
            if (tutorId == r.RequestedByUserId) return "The applicant cannot confirm their own request.";
            r.ConfirmationTutorUserId = tutorId;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (DbUpdateConcurrencyException) { return "This request changed. Reload before assigning."; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 1205) { return "Another reviewer changed this request. Reload."; }
        catch (SqlException ex) when (ex.Number == 1205) { return "Another reviewer changed this request. Reload."; }
    }
}
