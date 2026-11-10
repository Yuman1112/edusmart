using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Communication;
public sealed record EnrollmentNoticePayload(string Email, string Name, string Number, string Student,
    DateTime StartDate, int Months, string[] Classes, string? Reason, string? ContactUrl, bool Cancelled = false);
public sealed class EnrollmentNoticeService(ApplicationDbContext db, IMessageProtectionService protection, IConfiguration config)
{
    public async Task QueueAsync(EnrollmentApplication app, bool rejected, string? cancellationReason = null)
    {
        var ids = app.SelectedSubjects.Select(x => x.ClassSessionId).ToArray();
        var classes = await db.ClassSessions.AsNoTracking().Where(x => ids.Contains(x.ClassSessionId))
            .OrderBy(x => x.ClassSessionId).Select(x => x.ClassName ?? x.ClassCode).ToArrayAsync();
        classes = classes.Select(SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean).ToArray();
        string? contact = null;
        if (Uri.TryCreate(config["App:PublicBaseUrl"], UriKind.Absolute, out var origin) &&
            origin.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(origin.UserInfo))
            contact = origin.AbsoluteUri.TrimEnd('/') + "/GuestSupport";
        var recipients = new List<(string Role, string Name, string Email)> { ("Guardian", app.GuardianName, app.GuardianEmail) };
        if (!app.StudentHasNoEmail && !string.IsNullOrWhiteSpace(app.StudentEmail)) recipients.Add(("Student", app.StudentName, app.StudentEmail));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipient in recipients)
        {
            var email = recipient.Email.Trim();
            if (!MailAddress.TryCreate(email, out var address) || address.Address != email || !seen.Add(email)) continue;
            var payload = new EnrollmentNoticePayload(email, recipient.Name, app.ApplicationNumber, app.StudentName,
                app.RequestedStartDate, app.EnrollmentDurationMonths, classes, cancellationReason ?? (rejected ? app.RejectionReason : null), contact, cancellationReason != null);
            db.Set<EnrollmentNotice>().Add(new EnrollmentNotice {
                EnrollmentApplication = app, Kind = cancellationReason != null ? "Cancelled" : rejected ? "Rejected" : "Submitted", RecipientRole = recipient.Role,
                ProtectedPayload = protection.ProtectText(JsonSerializer.Serialize(payload)) });
        }
        // Caller saves with the application/status change in the SAME transaction.
    }
}
