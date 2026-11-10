
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Settings;

namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed class AccountSecurityMail
{
    private readonly EmailSettings _settings;

    public AccountSecurityMail(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken ct,
        bool isHtml = false)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(
                _settings.FromAddress,
                _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        message.To.Add(recipient);

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
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            await smtp.SendMailAsync(
                message,
                timeout.Token);
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            throw new SmtpException(
                "The mail request timed out.");
        }
    }
}
