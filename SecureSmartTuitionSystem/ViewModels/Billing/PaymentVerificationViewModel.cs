namespace SecureSmartTuitionSystem.ViewModels.Billing;
public sealed class PaymentVerificationViewModel
{
    public string SessionId { get; set; } = string.Empty;
    public string CallbackToken { get; set; } = string.Empty;
}
