
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

        string safeName = WebUtility.HtmlEncode(adminName);
        string safeUserId = WebUtility.HtmlEncode(adminUserId);
        string safeSetupUrl = WebUtility.HtmlEncode(setupUrl);

        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
            </head>
            <body style="margin:0;padding:30px;background:#f3f6fa;
                         font-family:Arial,sans-serif;">

                <div style="max-width:580px;margin:auto;background:white;
                            border-radius:12px;overflow:hidden;">

                    <div style="background:#153f56;color:white;padding:25px;">
                        <h2 style="margin:0;">EduSmart</h2>
                        <p style="margin:8px 0 0;">
                            Administrator Account Setup
                        </p>
                    </div>

                    <div style="padding:30px;color:#243746;">

                        <p>Hello {safeName},</p>

                        <p>
                            An administrator account has been
                            created for you in EduSmart.
                        </p>

                        <p>
                            <strong>Your Admin ID:</strong>
                            {safeUserId}
                        </p>

                        <p>
                            Please click the button below to
                            create your password and activate
                            your login access.
                        </p>

                        <p style="text-align:center;margin:30px 0;">
                            <a href="{safeSetupUrl}"
                               style="background:#087f8c;color:white;
                                      padding:13px 25px;
                                      text-decoration:none;
                                      border-radius:6px;
                                      font-weight:bold;">
                                Set Up My Account
                            </a>
                        </p>

                        <p>
                            This setup link is valid for a limited time.
                            If it expires, please contact your administrator
                            to request a new setup email.
                        </p>

                        <p>
                            If you did not expect this invitation,
                            please ignore this email and contact
                            EduSmart support.
                        </p>

                        <hr style="border:0;
                                   border-top:1px solid #e5e9ed;">

                        <p style="font-size:12px;color:#667;">
                            If the button does not work,
                            copy this link:
                        </p>

                        <p style="font-size:12px;
                                  overflow-wrap:anywhere;">
                            <a href="{safeSetupUrl}">
                                {safeSetupUrl}
                            </a>
                        </p>

                    </div>
                </div>
            </body>
            </html>
            """;

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
