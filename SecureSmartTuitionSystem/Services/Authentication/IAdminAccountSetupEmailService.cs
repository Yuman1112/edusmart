
namespace SecureSmartTuitionSystem.Services.Authentication;

public interface IAdminAccountSetupEmailService
{
    Task SendAsync(
        string recipientEmail,
        string adminName,
        string adminUserId,
        string setupUrl,
        CancellationToken cancellationToken = default);
}
