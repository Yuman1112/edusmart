namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TutorAvailabilityOverviewViewModel
{
    public int OutsideLessonCount { get; set; }
    public DateTime WeekStart { get; set; } = SecureSmartTuitionSystem.Services.Tutor.TutorAvailabilityPolicy.WeekStart(SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService.Today);
    public List<int> AdjustedDays { get; set; } = [];
    public bool IsWeeklyView { get; set; } = true;
    public SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType EmploymentType { get; set; }
    public List<int> LegacySlots { get; set; } = new();

    public string UserId { get; set; } = string.Empty;
    public string TutorId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<int> SelectedSlots { get; set; } = new();
}
