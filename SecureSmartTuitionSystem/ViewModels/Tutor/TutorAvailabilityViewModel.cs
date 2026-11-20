using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Models.Tutor;
namespace SecureSmartTuitionSystem.ViewModels.Tutor;
public sealed class TutorAvailabilityViewModel
{
    public int OutsideLessonCount { get; set; }
    public DateTime WeekStart { get; set; } = SecureSmartTuitionSystem.Services.Tutor.TutorAvailabilityPolicy.WeekStart(SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService.Today);
    public List<int> AdjustedDays { get; set; } = [];
    public bool IsWeeklyView { get; set; } = true;
    public List<int> SelectedSlots { get; set; } = new();
    public string Version { get; set; } = "";
    public bool RemoveLegacySlots { get; set; }
    [BindNever] public TutorEmploymentType EmploymentType { get; set; }
    [BindNever] public List<int> LegacySlots { get; set; } = new();
}
