using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class ChangeTutorStatusViewModel
{
    [Required, StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public bool? Activate { get; set; }

    [Required(ErrorMessage = "Please enter a reason.")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    [BindNever]
    public string TutorId { get; set; } = string.Empty;

    [BindNever]
    public string FullName { get; set; } = string.Empty;

    [BindNever]
    public bool SetupPending { get; set; }

    [BindNever]
    public List<string> BlockingAssignments { get; set; } = new();
}
