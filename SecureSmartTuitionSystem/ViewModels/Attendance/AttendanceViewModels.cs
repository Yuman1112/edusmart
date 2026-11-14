using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Attendance;
namespace SecureSmartTuitionSystem.ViewModels.Attendance;
public sealed class AttendanceIndexViewModel
{
    public string Sort { get; set; } = "date_asc";
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd => WeekStart.AddDays(6);
    public bool IsAdmin { get; set; }
    public List<AttendanceLessonRow> Lessons { get; set; } = [];
}
public sealed class AttendanceLessonRow
{
    public int ClassSessionId { get; set; }
    public DateTime Date { get; set; }
    public int? ChangeId { get; set; }
    public bool Replacement { get; set; }
    public int? SavedId { get; set; }
    public string ClassCode { get; set; } = "";
    public string ClassName { get => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(_className); set => _className = value; }
        private string _className = "";
    public string TutorName { get; set; } = "";
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string State { get; set; } = "";
}
public sealed class AttendanceSheetViewModel
{
    public int ClassSessionId { get; set; }
    public DateTime Date { get; set; }
    public int? ChangeId { get; set; }
    public bool Replacement { get; set; }
    public string RowVersion { get; set; } = "";
    public string ClassCode { get; set; } = "";
    public string ClassName { get => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(_className); set => _className = value; }
        private string _className = "";
    public string TutorName { get; set; } = "";
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string State { get; set; } = "";
    public bool CanEdit { get; set; }
    public bool IsLateEntry { get; set; }
    public bool CanRequestReopen { get; set; }
    public DateTime EditUntil { get; set; }
    [StringLength(500)] public string? LateSubmissionReason { get; set; }
    public List<AttendanceStudentRow> Students { get; set; } = [];
}
public sealed class AttendanceStudentRow
{
    public int StudentProfileId { get; set; }
    [ValidateNever] public string StudentNumber { get; set; } = "";
    [ValidateNever] public string StudentName { get; set; } = "";
    public AttendanceStatus? Status { get; set; }
    [StringLength(300)] public string? Remark { get; set; }
}
public sealed class SaveAttendanceViewModel
{
    public int ClassSessionId { get; set; }
    public DateTime Date { get; set; }
    public int? ChangeId { get; set; }
    public bool Replacement { get; set; }
    [StringLength(100)] public string? RowVersion { get; set; }
    public bool FinalSubmit { get; set; }
    [StringLength(500)] public string? LateSubmissionReason { get; set; }
    [MaxLength(100)] public List<AttendanceStudentRow> Students { get; set; } = [];
}
