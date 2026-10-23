namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TutorAvailabilityOverviewViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string TutorId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<int> SelectedSlots { get; set; } = new();
}
