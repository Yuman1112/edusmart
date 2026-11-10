using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Settings;
namespace SecureSmartTuitionSystem.Services.Communication;
public static class EnrollmentNoticeTemplate
{
    private static string Date(EnrollmentNoticePayload p) => p.StartDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
    public static string Subject(EnrollmentNoticePayload p, bool rejected) =>
        $"EduSmart enrollment application {(p.Cancelled ? "cancelled" : rejected ? "rejected" : "submitted")} - {p.Number}";
    public static string Text(EnrollmentNoticePayload p, bool rejected) =>
        $"Dear {p.Name},\n\n" +
        (p.Cancelled ? $"The enrollment application for {p.Student} has been cancelled. The previous payment link is no longer valid. Please do not use it to make payment." : rejected ? $"Following our review, the enrollment application for {p.Student} was not approved." :
            $"We have received the enrollment application for {p.Student}. It is now awaiting administrator review. No payment is required until approval.") +
        $"\n\nApplication: {p.Number}\nRequested start date: {Date(p)}\nEnrollment plan: {p.Months} month(s)\n\nSelected classes:\n" +
        string.Join("\n", p.Classes.Select((name, i) => $"{i + 1}. {name}")) +
        (rejected ? $"\n\nReason for {(p.Cancelled ? "cancellation" : "rejection")}:\n{p.Reason}" : "\n\nWe will contact you after the application has been reviewed.") +
        "\n\nFor further assistance, contact our administration team using Contact staff. No account or sign-up is required." +
        (p.ContactUrl == null ? " Visit the EduSmart website and open FAQ & Support > Contact staff." : "\n" + p.ContactUrl) +
        "\n\nKind regards,\nEduSmart Administration Team";
    public static string Html(EnrollmentNoticePayload p, bool rejected)
    {
        var heading = p.Cancelled ? "Enrollment application cancelled" : rejected ? "Enrollment application outcome" : "Your application has been received";
        var message = p.Cancelled ? $"The enrollment application for <strong>{E(p.Student)}</strong> has been cancelled. The previous payment link is no longer valid. Please do not use it to make payment." : rejected ? $"Following our review, the enrollment application for <strong>{E(p.Student)}</strong> was not approved." :
            $"Thank you for choosing EduSmart. We have received the enrollment application for <strong>{E(p.Student)}</strong>, and it is now awaiting administrator review.";
        var classes = string.Join("", p.Classes.Select(name => $"<li style='margin:8px 0;font-weight:600'>{E(name)}</li>"));
        var reason = rejected ? $"<div style='margin:24px 0;padding:18px;border-left:4px solid #b7424c;background:#fff4f5;border-radius:8px'><strong>Reason for {(p.Cancelled ? "cancellation" : "rejection")}</strong><div style='margin-top:8px;white-space:pre-wrap'>{E(p.Reason).Replace("\r\n", "\n").Replace("\n", "<br>")}</div></div>" :
            "<p style='padding:16px;background:#e9faf8;border-radius:8px'>No payment is required until your application is approved. We will contact you after the review.</p>";
        var contact = p.ContactUrl == null ? "<p>Visit the EduSmart website and open FAQ &amp; Support &gt; Contact staff.</p>" :
            $"<p style='margin:24px 0'><a href='{E(p.ContactUrl)}' style='display:inline-block;background:#0099a8;color:#fff;text-decoration:none;font-weight:700;padding:13px 20px;border-radius:8px'>Contact our administration team</a></p>";
        return $"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head><body style='margin:0;background:#f1fafb;color:#073b70;font-family:Arial,sans-serif;font-size:15px;line-height:1.6'><table role='presentation' width='100%' cellspacing='0' cellpadding='0'><tr><td align='center' style='padding:28px 12px'><table role='presentation' width='600' cellspacing='0' cellpadding='0' style='width:100%;max-width:600px;background:#fff;border:1px solid #cfe5ea;border-radius:14px;overflow:hidden'><tr><td style='padding:28px;background:#e7fafb'><div style='font-size:12px;font-weight:700;color:#008899;letter-spacing:1px'>EDUSMART ONLINE TUITION</div><h1 style='font-size:25px;line-height:1.3;margin:12px 0 0'>{heading}</h1></td></tr><tr><td style='padding:28px'><p>Dear {E(p.Name)},</p><p>{message}</p><div style='padding:18px;background:#f5fafb;border-radius:10px'><strong>Application:</strong> {E(p.Number)}<br><strong>Requested start date:</strong> {Date(p)}<br><strong>Enrollment plan:</strong> {p.Months} month(s)</div><h2 style='font-size:17px;margin:24px 0 8px'>Selected classes</h2><ol style='padding-left:22px'>{classes}</ol>{reason}<h2 style='font-size:17px;margin-top:24px'>How can we help?</h2><p>If you have questions about this application, our administration team will be happy to assist. You can submit an enquiry without creating an account or signing in.</p>{contact}<p style='margin-top:28px'>Kind regards,<br><strong>EduSmart Administration Team</strong></p></td></tr><tr><td style='padding:18px 28px;background:#f5fafb;font-size:12px;color:#617c95'>This email relates to application {E(p.Number)}. Keep this reference when contacting us.</td></tr></table></td></tr></table></body></html>";
    }
}
public interface IEnrollmentNoticeSender
{
    bool IsConfigured { get; }
    Task SendAsync(EnrollmentNoticePayload payload, bool rejected, CancellationToken ct);
}
public sealed class EnrollmentNoticeSender(IOptions<EmailSettings> options) : IEnrollmentNoticeSender
{
    private readonly EmailSettings s = options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(s.Host) && s.Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(s.UserName) && !string.IsNullOrWhiteSpace(s.Password) && MailAddress.TryCreate(s.FromAddress, out _);
    public async Task SendAsync(EnrollmentNoticePayload p, bool rejected, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Email is not configured.");
        using var mail = new MailMessage { From = new MailAddress(s.FromAddress, s.FromName),
            Subject = EnrollmentNoticeTemplate.Subject(p, rejected), SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8, Body = EnrollmentNoticeTemplate.Text(p, rejected), IsBodyHtml = false };
        mail.To.Add(new MailAddress(p.Email));
        mail.ReplyToList.Add(new MailAddress(s.FromAddress, s.FromName));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(EnrollmentNoticeTemplate.Html(p, rejected), Encoding.UTF8, "text/html"));
        using var smtp = new SmtpClient(s.Host, s.Port) { EnableSsl = s.UseSsl, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(s.UserName, s.Password) };
        await smtp.SendMailAsync(mail, ct);
    }
}
