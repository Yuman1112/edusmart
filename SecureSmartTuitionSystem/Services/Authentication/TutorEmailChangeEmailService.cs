using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Settings;

namespace SecureSmartTuitionSystem.Services.Authentication
{
    public sealed class TutorEmailChangeEmailService
        : ITutorEmailChangeEmailService
    {
        private readonly EmailSettings _settings;

        public TutorEmailChangeEmailService(
            IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(
            string recipientEmail,
            string tutorName,
            string tutorUserId,
            string verificationUrl,
            CancellationToken cancellationToken = default)
        {
            ValidateSettings();

            if (!Uri.TryCreate(
                    verificationUrl,
                    UriKind.Absolute,
                    out var verificationUri)
                || verificationUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(verificationUri.UserInfo))
            {
                throw new InvalidOperationException(
                    "The verification URL must be an absolute HTTPS URL.");
            }

            string safeTutorName =
                WebUtility.HtmlEncode(tutorName);

            string safeTutorUserId =
                WebUtility.HtmlEncode(tutorUserId);

            string safeVerificationUrl =
                WebUtility.HtmlEncode(verificationUrl);

            string htmlBody = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <title>Verify your new email address</title>
                </head>
                <body style="
                    margin:0;
                    padding:24px;
                    background:#f5fbfc;
                    font-family:Arial,sans-serif;
                    color:#06356b;">

                    <div style="
                        max-width:620px;
                        margin:0 auto;
                        background:#ffffff;
                        border:1px solid #cde5e8;
                        border-radius:16px;
                        overflow:hidden;">

                        <div style="
                            padding:26px 30px;
                            background:#e8fbfb;">

                            <div style="
                                font-size:14px;
                                font-weight:700;
                                color:#00899a;">
                                EDUSMART ONLINE TUITION
                            </div>

                            <h1 style="
                                margin:8px 0 0;
                                font-size:26px;">
                                Verify your new email address
                            </h1>
                        </div>

                        <div style="padding:28px 30px;">
                            <p>Dear {{safeTutorName}},</p>

                            <p>
                                A request was made to change the email
                                address for your EduSmart tutor account
                                to this address.
                            </p>

                            <p>
                                <strong>Tutor User ID:</strong>
                                {{safeTutorUserId}}
                            </p>

                            <p>
                                To confirm this change, open the link
                                below and follow the instructions.
                                Your account email will only change
                                after confirmation.
                            </p>

                            <p style="
                                margin:26px 0;
                                text-align:center;">
                                <a href="{{safeVerificationUrl}}"
                                   style="
                                       display:inline-block;
                                       padding:13px 24px;
                                       border-radius:10px;
                                       background:#0798a4;
                                       color:#ffffff;
                                       text-decoration:none;
                                       font-weight:700;">
                                    Review Email Change
                                </a>
                            </p>

                            <p style="font-size:13px;line-height:1.6;">
                                This link expires in two hours.
                                If a newer request is made, this link
                                will no longer be valid.
                            </p>

                            <p style="font-size:13px;line-height:1.6;">
                                If you were not expecting this change,
                                do not confirm it. Contact your
                                EduSmart administrator.
                                Please do not forward this email.
                            </p>

                            <p style="font-size:13px;line-height:1.6;">
                                If the button does not work, copy this
                                address into your browser:
                            </p>

                            <p style="
                                font-size:13px;
                                overflow-wrap:anywhere;">
                                <a href="{{safeVerificationUrl}}"
                                   style="color:#2563eb;">
                                    {{safeVerificationUrl}}
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
                Subject = "EduSmart: verify your new email address",
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(recipientEmail));

            using var smtpClient = new SmtpClient(
                _settings.Host,
                _settings.Port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _settings.UserName,
                    _settings.Password)
            };

            await smtpClient.SendMailAsync(
                message,
                cancellationToken);
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
                    "Email SMTP settings are incomplete.");
            }

            if (!_settings.UseSsl)
            {
                throw new InvalidOperationException(
                    "TLS must be enabled for email verification messages.");
            }
        }
    }
}