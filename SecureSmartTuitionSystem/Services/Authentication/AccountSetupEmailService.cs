using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Settings;

namespace SecureSmartTuitionSystem.Services.Authentication
{
    public sealed class AccountSetupEmailService
        : IAccountSetupEmailService
    {
        private readonly EmailSettings _settings;

        public AccountSetupEmailService(
            IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(
            string recipientEmail,
            string guardianName,
            string parentUserId,
            string studentName,
            string setupUrl,
            CancellationToken cancellationToken = default)
        {
            ValidateSettings();

            string safeGuardianName =
                WebUtility.HtmlEncode(guardianName);

            string safeParentUserId =
                WebUtility.HtmlEncode(parentUserId);

            string safeStudentName =
                WebUtility.HtmlEncode(studentName);

            string safeSetupUrl =
                WebUtility.HtmlEncode(setupUrl);

            string subject =
                $"EduSmart account ready - {safeParentUserId}";

            string htmlBody = $$"""
                <!doctype html>
                <html lang="en">
                <body style="
                    margin:0;
                    padding:24px;
                    background:#f2fbfc;
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
                                font-size:26px;
                                color:#06356b;">

                                Your EduSmart account is ready
                            </h1>
                        </div>

                        <div style="padding:28px 30px;">

                            <p style="margin-top:0;">
                                Dear {{safeGuardianName}},
                            </p>

                            <p>
                                Payment has been received and the enrollment
                                for
                                <strong>{{safeStudentName}}</strong>
                                has been completed successfully.
                            </p>

                            <div style="
                                margin:22px 0;
                                padding:18px;
                                background:#f5fbfc;
                                border-radius:12px;
                                line-height:1.8;">

                                <div>
                                    <strong>Parent User ID:</strong>
                                    {{safeParentUserId}}
                                </div>
                            </div>

                            <p>
                                Please create a password before signing in
                                to EduSmart.
                            </p>

                            <p style="
                                margin:26px 0;
                                text-align:center;">

                                <a href="{{safeSetupUrl}}"
                                   style="
                                       display:inline-block;
                                       padding:13px 24px;
                                       border-radius:10px;
                                       background:#0798a4;
                                       color:#ffffff;
                                       text-decoration:none;
                                       font-weight:700;">

                                    Set Up Your Account
                                </a>
                            </p>

                            <p style="
                                font-size:13px;
                                color:#60758f;
                                line-height:1.6;">

                                This account setup link is intended only
                                for the recipient of this email.
                                Please do not forward it.

                                <br><br>

                                If the button does not work, copy this
                                address into your browser:

                                <br>

                                <span style="overflow-wrap:anywhere;">
                                    {{safeSetupUrl}}
                                </span>
                            </p>

                        </div>
                    </div>

                </body>
                </html>
                """;

            using var message =
                new MailMessage
                {
                    From =
                        new MailAddress(
                            _settings.FromAddress,
                            _settings.FromName),

                    Subject = subject,

                    Body = htmlBody,

                    IsBodyHtml = true
                };

            message.To.Add(
                new MailAddress(recipientEmail));

            using var smtpClient =
                new SmtpClient(
                    _settings.Host,
                    _settings.Port)
                {
                    EnableSsl =
                        _settings.UseSsl,

                    UseDefaultCredentials =
                        false,

                    Credentials =
                        new NetworkCredential(
                            _settings.UserName,
                            _settings.Password)
                };

            await smtpClient.SendMailAsync(
                message,
                cancellationToken);
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(
                    _settings.Host) ||
                _settings.Port <= 0 ||
                string.IsNullOrWhiteSpace(
                    _settings.UserName) ||
                string.IsNullOrWhiteSpace(
                    _settings.Password) ||
                string.IsNullOrWhiteSpace(
                    _settings.FromAddress))
            {
                throw new InvalidOperationException(
                    "Email SMTP settings are incomplete.");
            }
        }
    }
}