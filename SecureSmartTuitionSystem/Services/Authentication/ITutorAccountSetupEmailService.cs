
namespace SecureSmartTuitionSystem.Services.Authentication
{
    public interface ITutorAccountSetupEmailService
    {
        Task SendAsync(
            string recipientEmail,
            string tutorName,
            string tutorUserId,
            string setupUrl,
            CancellationToken cancellationToken = default);
    }
}
