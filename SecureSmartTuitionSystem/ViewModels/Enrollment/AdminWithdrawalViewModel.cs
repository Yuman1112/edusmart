using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.ViewModels.Enrollment;
public sealed class AdminWithdrawalViewModel
{
    public int EnrollmentId { get; set; }
    public string Student { get; set; } = "";
    public string Class { get; set; } = "";
    public DateTime EffectiveDate { get; set; }
    public decimal EstimatedRefund { get; set; }
    [Required, StringLength(500, MinimumLength = 10)] public string Reason { get; set; } = "";
    public bool GuardianRequestConfirmed { get; set; }
    public bool BillingEffectConfirmed { get; set; }
    public string ConfirmationToken { get; set; } = "";
}
