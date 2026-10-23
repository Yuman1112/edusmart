namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TutorDetailsViewModel
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Qualification { get; set; } = "";
    public string? TeachingExperience { get; set; }
    public string AreaOfExpertise { get; set; } = "";

    public bool IsActive { get; set; }
    public bool SetupPending { get; set; }

    public List<TutorStatusHistoryItemViewModel> StatusHistory { get; set; } = new();
    public bool StatusHistoryHasUnverifiedEntries { get; set; }

    public string? PendingEmail { get; set; }
    public Guid? PendingEmailRequestId { get; set; }
    public DateTime? PendingEmailExpiresAtUtc { get; set; }

    public string? EmailChangeWatchToken { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<TutorTeachingLevelDetailsViewModel> TeachingLevels
    { get; set; } = new();
}

public sealed class TutorTeachingLevelDetailsViewModel
{
    public string EducationStage { get; set; } = "";
    public string AcademicLevel { get; set; } = "";
    public List<string> SubjectNames { get; set; } = new();
}
public sealed class TutorStatusHistoryItemViewModel
{
    public DateTime OccurredAtUtc { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}