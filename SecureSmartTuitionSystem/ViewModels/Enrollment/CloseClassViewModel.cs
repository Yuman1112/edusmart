using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.ViewModels.Enrollment;
public sealed class CloseClassViewModel
{
    public int ClassSessionId { get; set; }
    public string ClassName { get; set; } = "";
    [Required(ErrorMessage = "Reason field is required.")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = "";
    public string? BlockReason { get; set; }
}
