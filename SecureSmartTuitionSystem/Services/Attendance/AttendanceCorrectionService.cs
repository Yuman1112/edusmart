using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.ViewModels.Attendance;
namespace SecureSmartTuitionSystem.Services.Attendance;
public sealed class AttendanceCorrectionService(ApplicationDbContext db)
{
    public static bool ValidSort(string? sort) => sort is "requested_asc" or "requested_desc" or "lesson_asc" or "lesson_desc" or "student_asc" or "student_desc" or "status_asc" or "status_desc";
    public static IOrderedQueryable<AttendanceCorrectionRequest> Order(IQueryable<AttendanceCorrectionRequest> query, string sort)
    {
        var ordered = sort switch
        {
            "requested_desc" => query.OrderByDescending(x => x.RequestedAtUtc),
            "lesson_asc" => query.OrderBy(x => x.AttendanceRecord.AttendanceLesson.LessonDate).ThenBy(x => x.AttendanceRecord.AttendanceLesson.StartTime),
            "lesson_desc" => query.OrderByDescending(x => x.AttendanceRecord.AttendanceLesson.LessonDate).ThenByDescending(x => x.AttendanceRecord.AttendanceLesson.StartTime),
            "student_asc" => query.OrderBy(x => x.AttendanceRecord.StudentName).ThenBy(x => x.AttendanceRecord.StudentNumber),
            "student_desc" => query.OrderByDescending(x => x.AttendanceRecord.StudentName).ThenByDescending(x => x.AttendanceRecord.StudentNumber),
            "status_asc" => query.OrderBy(x => x.Decision),
            "status_desc" => query.OrderByDescending(x => x.Decision),
            _ => query.OrderBy(x => x.RequestedAtUtc)
        };
        return ordered.ThenBy(x => x.RequestedAtUtc).ThenBy(x => x.AttendanceCorrectionRequestId);
    }

    private IQueryable<AttendanceRecord> Requestable(string userId, string role) => role is "Tutor" or "Student"
        ? new AttendanceReportService(db).Visible(userId, role) : db.Set<AttendanceRecord>().Where(x => false);
    public async Task<AttendanceCorrectionViewModel?> CreateModelAsync(int recordId, string userId, string role)
    {
        return await Requestable(userId, role).Where(x => x.AttendanceRecordId == recordId).Select(x => new AttendanceCorrectionViewModel
        {
            RecordId = x.AttendanceRecordId,
            ClassCode = x.AttendanceLesson.ClassCode,
            Date = x.AttendanceLesson.LessonDate,
            Student = x.StudentNumber + " · " + x.StudentName,
            CurrentStatus = x.Status
        }).SingleOrDefaultAsync();
    }
    public async Task<string?> RequestAsync(int recordId, AttendanceStatus? requested, string? reason, string userId, string role, string actorName)
    {
        if (role is not ("Tutor" or "Student")) return "This account cannot request attendance corrections.";
        reason = reason?.Trim();
        if (!requested.HasValue || !Enum.IsDefined(requested.Value)) return "Select a valid attendance status.";
        if (string.IsNullOrEmpty(reason) || reason.Length > 500) return "Enter a correction reason of 1–500 characters.";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var record = await Requestable(userId, role).SingleOrDefaultAsync(x => x.AttendanceRecordId == recordId);
            if (record == null) return "This attendance record is not available for a correction request.";
            if (record.Status == requested) return "Choose a status different from the current attendance.";
            if (await db.Set<AttendanceCorrectionRequest>().AnyAsync(x => x.AttendanceRecordId == recordId && x.Decision == CorrectionDecision.Pending))
                return "This record already has a pending correction request.";
            db.Set<AttendanceCorrectionRequest>().Add(new AttendanceCorrectionRequest
            {
                AttendanceRecordId = recordId,
                RequestedByUserId = userId,
                RequestedByName = actorName[..Math.Min(actorName.Length, 256)],
                RequestedAtUtc = DateTime.UtcNow,
                PreviousStatus = record.Status,
                RequestedStatus = requested.Value,
                Reason = reason
            });
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627 or 1205)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Another request changed this record. Reload and try again."; }
        catch (SqlException ex) when (ex.Number == 1205)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Another request changed this record. Reload and try again."; }
    }
    public async Task<AttendanceCorrectionListViewModel> ListAsync(string userId, bool admin, string status, string sort = "requested_asc")
    {
        var query = db.Set<AttendanceCorrectionRequest>().AsNoTracking().Where(x => admin || x.RequestedByUserId == userId);
        if (status != "all")
        {
            var state = status == "approved" ? CorrectionDecision.Approved : status == "rejected" ? CorrectionDecision.Rejected : CorrectionDecision.Pending;
            query = query.Where(x => x.Decision == state);
        }
        var data = await Order(query, sort).Include(x => x.AttendanceRecord).ThenInclude(x => x.AttendanceLesson)
            .Take(500).ToListAsync();
        return new AttendanceCorrectionListViewModel
        {
            IsAdmin = admin,
            Status = status,
            Sort = sort,
            Rows = data.Select(x => new AttendanceCorrectionListRow
            {
                Id = x.AttendanceCorrectionRequestId,
                RecordId = x.AttendanceRecordId,
                Date = x.AttendanceRecord.AttendanceLesson.LessonDate,
                ClassCode = x.AttendanceRecord.AttendanceLesson.ClassCode,
                Student = x.AttendanceRecord.StudentNumber + " · " + x.AttendanceRecord.StudentName,
                RequestedBy = x.RequestedByName,
                RequestedAtUtc = x.RequestedAtUtc,
                PreviousStatus = x.PreviousStatus,
                RequestedStatus = x.RequestedStatus,
                CurrentStatus = x.AttendanceRecord.Status,
                Reason = x.Reason,
                Decision = x.Decision,
                ReviewedBy = x.ReviewedByName,
                ReviewedAtUtc = x.ReviewedAtUtc,
                ReviewRemark = x.ReviewRemark,
                RowVersion = Convert.ToBase64String(x.RowVersion)
            }).ToList()
        };
    }
    public async Task<string?> ReviewAsync(int id, string decision, string? remark, string? version, string adminId, string adminName)
    {
        remark = remark?.Trim();
        if (decision is not ("approve" or "reject")) return "Choose Approve or Reject.";
        if (string.IsNullOrEmpty(remark) || remark.Length > 500) return "Enter a review reason of 1–500 characters.";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            bool activeAdmin = await db.Users.AnyAsync(u => u.Id == adminId && u.IsActive && db.UserRoles.Any(ur => ur.UserId == u.Id
                && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")));
            if (!activeAdmin) return "Your account cannot review attendance corrections.";
            var request = await db.Set<AttendanceCorrectionRequest>().Include(x => x.AttendanceRecord).ThenInclude(x => x.AttendanceLesson)
                .SingleOrDefaultAsync(x => x.AttendanceCorrectionRequestId == id);
            if (request == null || request.Decision != CorrectionDecision.Pending) return "This request is no longer pending.";
            if (Convert.ToBase64String(request.RowVersion) != version) return "This request changed. Reload before reviewing again.";
            if (decision == "approve" && request.AttendanceRecord.Status != request.PreviousStatus)
                return "The attendance changed after this request. Reject this stale request and ask for a new one.";
            request.Decision = decision == "approve" ? CorrectionDecision.Approved : CorrectionDecision.Rejected;
            request.ReviewedByUserId = adminId; request.ReviewedByName = adminName[..Math.Min(adminName.Length, 256)];
            request.ReviewedAtUtc = DateTime.UtcNow; request.ReviewRemark = remark;
            if (decision == "approve")
            {
                request.AttendanceRecord.Status = request.RequestedStatus;
                request.AttendanceRecord.AttendanceLesson.UpdatedAtUtc = request.ReviewedAtUtc.Value;
                request.AttendanceRecord.AttendanceLesson.UpdatedByUserId = adminId;
            }
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "This request changed. Reload before reviewing again."; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 1205)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Another request is being reviewed. Reload and try again."; }
        catch (SqlException ex) when (ex.Number == 1205) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "Another request is being reviewed. Reload and try again."; }
    }
}
