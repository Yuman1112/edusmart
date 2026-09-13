namespace SecureSmartTuitionSystem.Services.Authentication
{
    public interface IAccountSetupEmailService
    {
        Task SendAsync(
            string recipientEmail,
            string guardianName,
            string parentUserId,
            string studentName,
            string setupUrl,
            CancellationToken cancellationToken = default);
    }
}