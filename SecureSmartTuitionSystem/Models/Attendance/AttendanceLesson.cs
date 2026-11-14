using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Models.Attendance;
public enum AttendanceStatus { Present = 1, Absent = 2, Late = 3 }
// One dated occurrence, not the entire recurring class.
public sealed class AttendanceLesson
{
    public int AttendanceLessonId { get; set; }
    public int ClassSessionId { get; set; }
    public DateTime OriginalDate { get; set; }
    public bool IsReplacement { get; set; }
    public int? ClassLessonChangeId { get; set; }
    public DateTime LessonDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    [MaxLength(450)] public string TutorUserId { get; set; } = "";
    [MaxLength(30)] public string ClassCode { get; set; } = "";
    [MaxLength(200)] public string ClassName { get => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(_className); set => _className = value; }
        private string _className = "";
    [MaxLength(100)] public string TutorName { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
    [MaxLength(450)] public string UpdatedByUserId { get; set; } = "";
    public DateTime? SubmittedAtUtc { get; set; }
    public bool SubmittedLate { get; set; }
    [MaxLength(500)] public string? LateSubmissionReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ClassSession ClassSession { get; set; } = null!;
    public ClassLessonChange? ClassLessonChange { get; set; }
    public ApplicationUser TutorUser { get; set; } = null!;
    public List<AttendanceRecord> Records { get; set; } = [];
}
public sealed class AttendanceRecord
{
    public int AttendanceRecordId { get; set; }
    public int AttendanceLessonId { get; set; }
    public int StudentProfileId { get; set; }
    [MaxLength(20)] public string StudentNumber { get; set; } = "";
    [MaxLength(100)] public string StudentName { get; set; } = "";
    public AttendanceStatus? Status { get; set; }
    [MaxLength(300)] public string? Remark { get; set; }
    public AttendanceLesson AttendanceLesson { get; set; } = null!;
    public StudentProfile StudentProfile { get; set; } = null!;
}
