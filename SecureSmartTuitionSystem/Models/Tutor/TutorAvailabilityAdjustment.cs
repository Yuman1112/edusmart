using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Models.Tutor;
public sealed class TutorAvailabilityAdjustment
{
    public int ClassLessonChangeId { get; set; }
    public ClassLessonChange ClassLessonChange { get; set; } = null!;
    [Required, MaxLength(450)] public string TutorUserId { get; set; } = "";
    public bool BlocksOriginal { get; set; }
    public DateTime? AddedDate { get; set; }
    public TimeOnly? AddedStart { get; set; }
    public TimeOnly? AddedEnd { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [Required, MaxLength(450)] public string ChangedByUserId { get; set; } = "";
}
