using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.ViewModels.Tutor;
public sealed class TutorSchedulingRequest
{
    public EducationStage? EducationStage { get; set; }
    [StringLength(20)] public string AcademicLevel { get; set; } = "";
    [StringLength(10)] public string SubjectName { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DurationMinutes { get; set; } = 120;
    [StringLength(450)] public string? TutorUserId { get; set; }
}
public sealed record TutorSchedulingChoice(string Id, string Label);
public sealed record TutorWeeklySlot(string Key, string Day, int DayNumber, string Start, string End, string FirstDate, int LessonCount);
