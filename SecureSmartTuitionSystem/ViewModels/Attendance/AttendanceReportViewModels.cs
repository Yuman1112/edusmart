using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Attendance;
namespace SecureSmartTuitionSystem.ViewModels.Attendance;
public sealed class AttendanceReportFilter
{
    [DataType(DataType.Date)] public DateTime From { get; set; } = DateTime.UtcNow.AddHours(8).Date.AddDays(-30);
    [DataType(DataType.Date)] public DateTime To { get; set; } = DateTime.UtcNow.AddHours(8).Date;
    public int? ClassId { get; set; }
    public int? StudentId { get; set; }
    [RegularExpression(@"^(1|2|3|unrecorded)?$")] public string? Status { get; set; }
}
public sealed class AttendanceChoice
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
}
public sealed class AttendanceReportRow
{
    public int RecordId { get; set; }
    public int ClassId { get; set; }
    public DateTime Date { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string ClassCode { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Tutor { get; set; } = "";
    public string StudentNumber { get; set; } = "";
    public string StudentName { get; set; } = "";
    public AttendanceStatus? Status { get; set; }
    public string? Remark { get; set; }
    public bool Replacement { get; set; }
    public bool ScheduleChanged { get; set; }
    public bool CanRequest { get; set; }
}
public sealed class AttendanceReportViewModel
{
    public AttendanceReportFilter Filter { get; set; } = new();
    public List<AttendanceChoice> Classes { get; set; } = [];
    public List<AttendanceChoice> Students { get; set; } = [];
    public List<AttendanceReportRow> Rows { get; set; } = [];
    public int Total { get; set; }
    public bool CanExport { get; set; }
    public bool CanCorrect { get; set; }
    public bool IsPrint { get; set; }
    public bool ExportLimitExceeded => Total > 5000;
}
public sealed class AttendanceCorrectionViewModel
{
    public int RecordId { get; set; }
    public string ClassCode { get; set; } = "";
    public DateTime Date { get; set; }
    public string Student { get; set; } = "";
    public AttendanceStatus? CurrentStatus { get; set; }
    public AttendanceStatus? RequestedStatus { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class AttendanceCorrectionListViewModel
{
    public bool IsAdmin { get; set; }
    public string Status { get; set; } = "pending";
    public List<AttendanceCorrectionListRow> Rows { get; set; } = [];
}
public sealed class AttendanceCorrectionListRow
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public DateTime Date { get; set; }
    public string ClassCode { get; set; } = "";
    public string Student { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
    public AttendanceStatus? PreviousStatus { get; set; }
    public AttendanceStatus RequestedStatus { get; set; }
    public AttendanceStatus? CurrentStatus { get; set; }
    public string Reason { get; set; } = "";
    public CorrectionDecision Decision { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewRemark { get; set; }
    public string RowVersion { get; set; } = "";
}
