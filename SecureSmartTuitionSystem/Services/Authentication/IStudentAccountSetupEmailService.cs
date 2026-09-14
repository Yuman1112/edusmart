namespace SecureSmartTuitionSystem.Services.Authentication
{
    public interface IStudentAccountSetupEmailService
    {
        Task SendAsync(
            string recipientEmail,
            string studentName,
            string studentUserId,
            string setupUrl,
            bool sentToGuardian,
            CancellationToken cancellationToken = default);
    }
}