using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class ConfirmTutorEmailChangeViewModel
{
    [Required]
    public string UserId { get; set; } = "";

    public Guid RequestId { get; set; }

    [Required]
    public string Token { get; set; } = "";

    // Display values are loaded by the Controller.
    public string TutorId { get; set; } = "";

    public string NewEmail { get; set; } = "";

    public bool CanConfirm { get; set; }

    public bool Confirmed { get; set; }

    public string? ErrorMessage { get; set; }

    public string? InfoMessage { get; set; }
}