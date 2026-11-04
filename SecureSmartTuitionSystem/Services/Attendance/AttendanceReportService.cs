using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Attendance;
namespace SecureSmartTuitionSystem.Services.Attendance;
public sealed class AttendanceReportService(ApplicationDbContext db)
{
    public static bool ValidSort(string? sort) => sort is "date_asc" or "date_desc" or "class_asc" or "class_desc" or "student_asc" or "student_desc" or "status_asc" or "status_desc";
    public static IOrderedQueryable<AttendanceRecord> Order(IQueryable<AttendanceRecord> query, string sort)
    {
        var ordered = sort switch
        {
            "date_asc" => query.OrderBy(x => x.AttendanceLesson.LessonDate).ThenBy(x => x.AttendanceLesson.StartTime),
            "class_asc" => query.OrderBy(x => x.AttendanceLesson.ClassCode).ThenBy(x => x.AttendanceLesson.TutorName),
            "class_desc" => query.OrderByDescending(x => x.AttendanceLesson.ClassCode).ThenByDescending(x => x.AttendanceLesson.TutorName),
            "student_asc" => query.OrderBy(x => x.StudentName).ThenBy(x => x.StudentNumber),
            "student_desc" => query.OrderByDescending(x => x.StudentName).ThenByDescending(x => x.StudentNumber),
            "status_asc" => query.OrderBy(x => x.Status == null ? 4 : (int)x.Status.Value),
            "status_desc" => query.OrderByDescending(x => x.Status == null ? 4 : (int)x.Status.Value),
            _ => query.OrderByDescending(x => x.AttendanceLesson.LessonDate).ThenByDescending(x => x.AttendanceLesson.StartTime)
        };
        return ordered.ThenByDescending(x => x.AttendanceLesson.LessonDate).ThenBy(x => x.AttendanceLesson.StartTime).ThenBy(x => x.AttendanceLesson.ClassCode).ThenBy(x => x.StudentNumber).ThenBy(x => x.AttendanceRecordId);
    }

    public static bool Valid(AttendanceReportFilter filter) => ValidSort(filter.Sort) && filter.From.Year >= 2000 && filter.To.Year <= 2100
        && filter.From.Date <= filter.To.Date && (filter.To.Date - filter.From.Date).TotalDays <= 366
        && (string.IsNullOrEmpty(filter.Status) || filter.Status is "1" or "2" or "3" or "unrecorded")
        && (!filter.ClassId.HasValue || filter.ClassId > 0) && (!filter.StudentId.HasValue || filter.StudentId > 0);
    // Used for all reads and exports: role filtering always happens in SQL.
    public IQueryable<AttendanceRecord> Visible(string userId, string role)
    {
        var records = db.Set<AttendanceRecord>().AsNoTracking();
        var now = ClassScheduleService.Now; var today = now.Date; var time = TimeOnly.FromDateTime(now);
        records = records.Where(x => x.AttendanceLesson.SubmittedAtUtc != null || x.AttendanceLesson.LessonDate < today
            || (x.AttendanceLesson.LessonDate == today && x.AttendanceLesson.EndTime <= time));
        return role switch
        {
            "Admin" => records,
            "Tutor" => records.Where(x => x.AttendanceLesson.TutorUserId == userId),
            "Student" => records.Where(x => x.StudentProfile.ApplicationUserId == userId),
            "Parent" => records.Where(x => x.StudentProfile.ParentStudents.Any(p => p.IsActive && p.ParentProfile.ApplicationUserId == userId && p.ParentProfile.ApplicationUser.IsActive)),
            _ => records.Where(x => false)
        };
    }
    public async Task<AttendanceReportViewModel> GetAsync(AttendanceReportFilter filter, string userId, string role, bool full = false)
    {
        var visible = Visible(userId, role);
        var model = new AttendanceReportViewModel { Filter = filter, CanExport = role is "Admin" or "Tutor", CanCorrect = role is "Tutor" or "Student", IsPrint = full };
        model.Classes = await visible.Select(x => new AttendanceChoice { Id = x.AttendanceLesson.ClassSessionId, Text = x.AttendanceLesson.ClassCode })
            .Distinct().OrderBy(x => x.Text).ToListAsync();
        model.Students = await visible.Select(x => new AttendanceChoice { Id = x.StudentProfileId, Text = x.StudentNumber + " · " + x.StudentName })
            .Distinct().OrderBy(x => x.Text).ToListAsync();
        var query = visible.Where(x => x.AttendanceLesson.LessonDate >= filter.From.Date && x.AttendanceLesson.LessonDate <= filter.To.Date);
        if (filter.ClassId.HasValue) query = query.Where(x => x.AttendanceLesson.ClassSessionId == filter.ClassId);
        if (filter.StudentId.HasValue) query = query.Where(x => x.StudentProfileId == filter.StudentId);
        if (filter.Status == "unrecorded") query = query.Where(x => x.Status == null);
        else if (!string.IsNullOrEmpty(filter.Status)) { var status = (AttendanceStatus)int.Parse(filter.Status); query = query.Where(x => x.Status == status); }
        model.Total = await query.CountAsync();
        if (full && model.ExportLimitExceeded) return model;
        model.Rows = await Order(query, filter.Sort).Take(full ? 5000 : 500)
            .Select(x => new AttendanceReportRow
            {
                RecordId = x.AttendanceRecordId,
                ClassId = x.AttendanceLesson.ClassSessionId,
                Date = x.AttendanceLesson.LessonDate,
                Start = x.AttendanceLesson.StartTime,
                End = x.AttendanceLesson.EndTime,
                ClassCode = x.AttendanceLesson.ClassCode,
                ClassName = x.AttendanceLesson.ClassName,
                Tutor = x.AttendanceLesson.TutorName,
                StudentNumber = x.StudentNumber,
                StudentName = x.StudentName,
                Status = x.Status,
                Remark = x.Remark,
                Replacement = x.AttendanceLesson.IsReplacement,
                // Saved records remain visible after schedule changes but are flagged.
                ScheduleChanged = x.AttendanceLesson.ClassSession.Status == ClassSessionStatus.Cancelled
                    || x.AttendanceLesson.ClassSession.TutorUserId != x.AttendanceLesson.TutorUserId
                    || (!x.AttendanceLesson.IsReplacement && db.ClassLessonChanges.Any(c => c.ClassSessionId == x.AttendanceLesson.ClassSessionId && c.OriginalDate == x.AttendanceLesson.OriginalDate))
                    || (x.AttendanceLesson.IsReplacement && (x.AttendanceLesson.ClassLessonChange!.ReplacementDate != x.AttendanceLesson.LessonDate
                        || x.AttendanceLesson.ClassLessonChange.ReplacementStartTime != x.AttendanceLesson.StartTime
                        || x.AttendanceLesson.ClassLessonChange.ReplacementEndTime != x.AttendanceLesson.EndTime)),
                CanRequest = (role == "Tutor" && x.AttendanceLesson.TutorUserId == userId) || (role == "Student" && x.StudentProfile.ApplicationUserId == userId)
            }).ToListAsync();
        return model;
    }
}