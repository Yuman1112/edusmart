namespace SecureSmartTuitionSystem.Services.Billing
{
    public interface IFirstPaymentEmailService
    {
        Task SendAsync(
            string recipientEmail,
            string guardianName,
            string studentName,
            string applicationNumber,
            string billNumber,
            decimal totalAmount,
            DateTime dueAt,
            string paymentUrl,
            CancellationToken cancellationToken = default);
    }
}