using SecureSmartTuitionSystem.Models.Billing;

namespace SecureSmartTuitionSystem.Services.Billing
{
    public interface IFirstPaymentEmailService
    {
        Task SendAsync(
            string recipientEmail,
            string guardianName,
            string studentName,
            Bill bill,
            string paymentUrl,
            CancellationToken cancellationToken = default);
    }
}
