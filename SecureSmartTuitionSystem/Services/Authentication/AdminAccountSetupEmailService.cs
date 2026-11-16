
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Settings;

namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed class AdminAccountSetupEmailService
    : IAdminAccountSetupEmailService
{
    private readonly EmailSettings _settings;

    public AdminAccountSetupEmailService(
        IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendAsync(
        string recipientEmail,
        string adminName,
        string adminUserId,
        string setupUrl,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        string html = SecurityEmailTemplates.AdminAccountSetup(
            adminName, adminUserId, setupUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(
                _settings.FromAddress,
                _settings.FromName),
            Subject =
                $"EduSmart Administrator Account Setup - {adminUserId}",
            Body = html,
            IsBodyHtml = true
        };

        message.To.Add(recipientEmail);

        using var smtp = new SmtpClient(
            _settings.Host,
            _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                _settings.UserName,
                _settings.Password)
        };

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            await smtp.SendMailAsync(
                message,
                timeout.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new SmtpException(
                "The admin setup email request timed out.");
        }
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host)
            || _settings.Port <= 0
            || string.IsNullOrWhiteSpace(_settings.UserName)
            || string.IsNullOrWhiteSpace(_settings.Password)
            || string.IsNullOrWhiteSpace(_settings.FromAddress))
        {
            throw new InvalidOperationException(
                "Admin setup email SMTP settings are incomplete.");
        }
    }
}

