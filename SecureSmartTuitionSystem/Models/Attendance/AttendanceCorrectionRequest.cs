using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Attendance;
public enum CorrectionDecision { Pending, Approved, Rejected }
public sealed class AttendanceCorrectionRequest
{
    public int AttendanceCorrectionRequestId { get; set; }
    public int AttendanceRecordId { get; set; }
    [MaxLength(450)] public string RequestedByUserId { get; set; } = "";
    [MaxLength(256)] public string RequestedByName { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
    public AttendanceStatus? PreviousStatus { get; set; }
    public AttendanceStatus RequestedStatus { get; set; }
    [MaxLength(500)] public string Reason { get; set; } = "";
    public CorrectionDecision Decision { get; set; }
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(256)] public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    [MaxLength(500)] public string? ReviewRemark { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public AttendanceRecord AttendanceRecord { get; set; } = null!;
}