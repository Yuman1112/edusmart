using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Models.Attendance;
public enum AttendanceReopenDecision { Pending, Approved, Rejected }
public sealed class AttendanceReopenRequest
{
    public int AttendanceReopenRequestId { get; set; }
    public int ClassSessionId { get; set; }
    public DateTime OriginalDate { get; set; }
    public DateTime LessonDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsReplacement { get; set; }
    public int? ChangeId { get; set; }
    [MaxLength(450)] public string TutorUserId { get; set; } = "";
    [MaxLength(200)] public string ClassName { get; set; } = "";
    [MaxLength(100)] public string TutorName { get; set; } = "";
    [MaxLength(500)] public string Reason { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
    public AttendanceReopenDecision Decision { get; set; }
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(500)] public string? ReviewRemark { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? OpenUntilUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ClassSession ClassSession { get; set; } = null!;
}
