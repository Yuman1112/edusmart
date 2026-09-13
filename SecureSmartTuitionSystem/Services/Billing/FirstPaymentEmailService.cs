using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Settings;

namespace SecureSmartTuitionSystem.Services.Billing
{
    public sealed class FirstPaymentEmailService
        : IFirstPaymentEmailService
    {
        private readonly EmailSettings _settings;

        public FirstPaymentEmailService(
            IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(
            string recipientEmail,
            string guardianName,
            string studentName,
            Bill bill,
            string paymentUrl,
            CancellationToken cancellationToken = default)
        {
            ValidateSettings();

            string safeGuardianName =
                WebUtility.HtmlEncode(guardianName);

            string safeStudentName =
                WebUtility.HtmlEncode(studentName);

            string safePaymentUrl =
                WebUtility.HtmlEncode(paymentUrl);

            string subject =
                $"EduSmart payment required for {studentName}";

            var itemRows = new StringBuilder();

            foreach (BillItem item in
                     bill.Items.OrderBy(item => item.BillItemId))
            {
                string safeDescription =
                    WebUtility.HtmlEncode(item.Description);

                string lineTotalColour =
                    item.LineTotal < 0
                        ? "#008f5a"
                        : "#06356b";

                itemRows.Append($$"""
                    <tr>
                        <td style="padding:14px 16px;border-bottom:1px solid #d7e8eb;font-weight:700;">
                            {{safeDescription}}
                        </td>
                        <td style="padding:14px 12px;border-bottom:1px solid #d7e8eb;text-align:center;">
                            {{item.Quantity}}
                        </td>
                        <td style="padding:14px 16px;border-bottom:1px solid #d7e8eb;text-align:right;white-space:nowrap;">
                            RM {{item.UnitAmount:F2}}
                        </td>
                        <td style="padding:14px 16px;border-bottom:1px solid #d7e8eb;text-align:right;white-space:nowrap;font-weight:700;color:{{lineTotalColour}};">
                            RM {{item.LineTotal:F2}}
                        </td>
                    </tr>
                    """);
            }

            string discountSummaryHtml = string.Empty;

            if (bill.TuitionDiscountAmount > 0)
            {
                discountSummaryHtml = $$"""
                    <tr>
                        <td style="padding:7px 0;color:#60758f;">
                            Tuition Discount ({{bill.TuitionDiscountRate:F0}}%)
                        </td>
                        <td style="padding:7px 0;text-align:right;color:#008f5a;font-weight:700;white-space:nowrap;">
                            RM &minus;{{bill.TuitionDiscountAmount:F2}}
                        </td>
                    </tr>
                    """;
            }

            string hardcopySummaryHtml = string.Empty;

            if (bill.HardcopyFee > 0)
            {
                hardcopySummaryHtml = $$"""
                    <tr>
                        <td style="padding:7px 0;color:#60758f;">Hardcopy Materials</td>
                        <td style="padding:7px 0;text-align:right;font-weight:700;white-space:nowrap;">
                            RM {{bill.HardcopyFee:F2}}
                        </td>
                    </tr>
                    """;
            }

            string deliverySummaryHtml = string.Empty;

            if (bill.DeliveryFee > 0)
            {
                deliverySummaryHtml = $$"""
                    <tr>
                        <td style="padding:7px 0;color:#60758f;">Delivery Fee</td>
                        <td style="padding:7px 0;text-align:right;font-weight:700;white-space:nowrap;">
                            RM {{bill.DeliveryFee:F2}}
                        </td>
                    </tr>
                    """;
            }

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

                                Complete the first payment
                            </h1>
                        </div>

                        <div style="padding:28px 30px;">
                            <p style="margin-top:0;">
                                Dear {{safeGuardianName}},
                            </p>

                            <p>
                                The enrollment application for
                                <strong>{{safeStudentName}}</strong>
                                has been approved and is awaiting payment.
                            </p>

                            <div style="margin:22px 0 12px;font-size:18px;font-weight:700;color:#06356b;">
                                Bill Items
                            </div>

                            <div style="overflow-x:auto;border:1px solid #d7e8eb;border-radius:12px;">
                                <table role="presentation" style="width:100%;border-collapse:collapse;font-size:14px;">
                                    <thead>
                                        <tr style="background:#f1fafb;color:#006f7f;">
                                            <th style="padding:13px 16px;text-align:left;">Description</th>
                                            <th style="padding:13px 12px;text-align:center;">Quantity</th>
                                            <th style="padding:13px 16px;text-align:right;">Unit Amount</th>
                                            <th style="padding:13px 16px;text-align:right;">Total</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {{itemRows}}
                                    </tbody>
                                </table>
                            </div>

                            <table role="presentation" style="width:100%;max-width:390px;margin:20px 0 0 auto;border-collapse:collapse;font-size:14px;">
                                <tr>
                                    <td style="padding:7px 0;color:#60758f;">Tuition Subtotal</td>
                                    <td style="padding:7px 0;text-align:right;font-weight:700;white-space:nowrap;">
                                        RM {{bill.TuitionSubtotal:F2}}
                                    </td>
                                </tr>

                                {{discountSummaryHtml}}
                                {{hardcopySummaryHtml}}
                                {{deliverySummaryHtml}}

                                <tr>
                                    <td style="padding:15px 0 4px;border-top:1px solid #d7e8eb;font-size:17px;font-weight:700;">
                                        Total Amount
                                    </td>
                                    <td style="padding:15px 0 4px;border-top:1px solid #d7e8eb;text-align:right;font-size:17px;font-weight:700;white-space:nowrap;">
                                        RM {{bill.TotalAmount:F2}}
                                    </td>
                                </tr>
                            </table>

                            <div style="margin-top:18px;padding:13px 16px;background:#f5fbfc;border-radius:10px;">
                                <strong>Due date:</strong>
                                {{bill.DueAt.ToLocalTime():dd MMM yyyy}}
                            </div>

                            <p style="
                                margin:26px 0;
                                text-align:center;">

                                <a href="{{safePaymentUrl}}"
                                   style="
                                       display:inline-block;
                                       padding:13px 24px;
                                       border-radius:10px;
                                       background:#0798a4;
                                       color:#ffffff;
                                       text-decoration:none;
                                       font-weight:700;">

                                    Open Secure Payment Page
                                </a>
                            </p>

                            <p style="
                                font-size:13px;
                                color:#60758f;
                                line-height:1.6;">

                                This link is intended for the recipient
                                of this email. Please do not forward it.

                                <br><br>

                                If the button does not work, copy this
                                address into your browser:

                                <br>

                                <span style="overflow-wrap:anywhere;">
                                    {{safePaymentUrl}}
                                </span>
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
                    EnableSsl = _settings.UseSsl,

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
            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                _settings.Port <= 0 ||
                string.IsNullOrWhiteSpace(_settings.UserName) ||
                string.IsNullOrWhiteSpace(_settings.Password) ||
                string.IsNullOrWhiteSpace(_settings.FromAddress))
            {
                throw new InvalidOperationException(
                    "Email SMTP settings are incomplete.");
            }
        }
    }
}
