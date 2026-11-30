using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Settings;
namespace SecureSmartTuitionSystem.Services.Communication;
public interface IGuestEnquiryEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string recipient, string name, string question, string reply, int enquiryId, CancellationToken ct);
}
public sealed class GuestEnquiryEmailSender(IOptions<EmailSettings> options) : IGuestEnquiryEmailSender
{
    private readonly EmailSettings settings = options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Host) && settings.Port is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(settings.UserName) && !string.IsNullOrWhiteSpace(settings.Password)
        && MailAddress.TryCreate(settings.FromAddress, out _);
    public async Task SendAsync(string recipient, string name, string question, string reply, int enquiryId, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Email is not configured.");
        using var mail = new MailMessage { From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = GuestEnquiryEmailTemplate.Subject,
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = false,
            Body = GuestEnquiryEmailTemplate.PlainText(name, question, reply, enquiryId) };
        mail.To.Add(new MailAddress(recipient));
        mail.ReplyToList.Add(new MailAddress(settings.FromAddress, settings.FromName));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            GuestEnquiryEmailTemplate.Html(name, question, reply, enquiryId),
            System.Text.Encoding.UTF8, "text/html"));
        using var smtp = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.UseSsl,
            UseDefaultCredentials = false, Credentials = new NetworkCredential(settings.UserName, settings.Password) };
        await smtp.SendMailAsync(mail, ct);
    }
}
