namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TutorAvailabilityViewModel
{
    // Value = day number * 48 + half-hour slot number.
    public List<int> SelectedSlots { get; set; } = new();
}