namespace SecureSmartTuitionSystem.Services.Authentication
{
    public interface ITutorEmailChangeEmailService
    {
        Task SendAsync(
            string recipientEmail,
            string tutorName,
            string tutorUserId,
            string verificationUrl,
            CancellationToken cancellationToken = default);
    }
}