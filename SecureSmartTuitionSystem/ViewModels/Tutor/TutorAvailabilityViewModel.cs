using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Models.Tutor;
namespace SecureSmartTuitionSystem.ViewModels.Tutor;
public sealed class TutorAvailabilityViewModel
{
    public List<int> SelectedSlots { get; set; } = new();
    public bool RemoveLegacySlots { get; set; }
    [BindNever] public TutorEmploymentType EmploymentType { get; set; }
    [BindNever] public List<int> LegacySlots { get; set; } = new();
}
