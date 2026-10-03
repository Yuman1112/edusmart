using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.ViewModels.Enrollment;
public sealed class LearningClassesModel
{
    public List<StudentProfile> Children { get; set; } = new();
    public StudentProfile? Student { get; set; }
    public CalendarModel Calendar { get; set; } = new();
    public DateTime? NextStartDate { get; set; }
    public List<StudentEnrollment> Enrollments { get; set; } = new();
    public List<LessonItem> Lessons { get; set; } = new();
}
