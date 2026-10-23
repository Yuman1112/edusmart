using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TutorWorkloadViewModel
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd => WeekStart.AddDays(6);
    public string? SelectedUserId { get; set; }
    public List<TutorWorkloadRowViewModel> AllTutors { get; set; } = new();
    public List<TutorWorkloadRowViewModel> Rows { get; set; } = new();
    public List<LessonItem> Lessons { get; set; } = new();
    public int TotalLessons => Rows.Sum(row => row.LessonCount);
    public decimal TotalHours => Rows.Sum(row => row.TeachingHours);
}

public sealed class TutorWorkloadRowViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string TutorId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ClassCount { get; set; }
    public int RegularLessonCount { get; set; }
    public int ReplacementCount { get; set; }
    public int LessonCount => RegularLessonCount + ReplacementCount;
    public decimal TeachingHours { get; set; }
}