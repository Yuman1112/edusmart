using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.ViewModels.Enrollment;
public sealed class LessonItem
{
    public ClassSession Class { get; set; } = null!;
    public DateTime Date { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string Venue { get; set; } = "";
    public bool Cancelled { get; set; }
    public bool Replacement { get; set; }
    public int? ChangeId { get; set; }
    public DateTime OriginalDate { get; set; }
    public string Reason { get; set; } = "";
    public string Status => Cancelled ? "Cancelled" : Date.Add(End.ToTimeSpan()) < DateTime.UtcNow.AddHours(8) ? "Finished" : Replacement ? "Replacement" : "Scheduled";
}
public sealed class CalendarModel
{
    public DateTime Anchor { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Mode { get; set; } = "month";
    public int? ClassSessionId { get; set; }
    public List<LessonItem> Lessons { get; set; } = new();
}
public sealed class CancelLessonModel
{
    public int ClassSessionId { get; set; }
    public DateTime Date { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
public sealed class ReplacementModel
{
    public int ChangeId { get; set; }
    [Required, DataType(DataType.Date)] public DateTime? Date { get; set; }
    [Required, DataType(DataType.Time)] public TimeOnly? Start { get; set; }
    [Required, DataType(DataType.Time)] public TimeOnly? End { get; set; }
    [Required, Url, StringLength(250)] public string Venue { get; set; } = "";
    public ClassLessonChange? Original { get; set; }
}
public sealed class ClassMembersModel
{
    public int ClassSessionId { get; set; }
    public bool CanWithdraw { get; set; }
    public List<StudentEnrollment> Members { get; set; } = new();
}
