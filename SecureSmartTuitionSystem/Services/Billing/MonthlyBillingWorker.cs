using System.Data;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Settings;
namespace SecureSmartTuitionSystem.Services.Billing;
public sealed class MonthlyBillingWorker(IServiceScopeFactory scopes, ILogger<MonthlyBillingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                try { await scope.ServiceProvider.GetRequiredService<MonthlyBillingService>().GenerateCurrentAsync(stoppingToken); }
                catch (Exception e) when (e is not OperationCanceledException) { logger.LogError(e, "Monthly bill generation requires review; existing bill notifications will still run."); }
                await NotifyAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "Monthly billing/notifications failed; next run will retry safely."); }
            try { await Task.Delay(NextDelay(DateTime.UtcNow), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
    // Wake at the Malaysia month boundary even when the last hourly run was at :30.
    // A running host is still required; after downtime only the current month is generated.
    public static TimeSpan NextDelay(DateTime utcNow)
    {
        var nextMonthUtc = MonthlyBillingService.MonthOf(utcNow.AddHours(8)).AddMonths(1).AddHours(-8);
        return TimeSpan.FromSeconds(Math.Clamp((nextMonthUtc - utcNow).TotalSeconds, 1, 3600));
    }
    public static bool EmailKind(string kind) => kind is "bill-issued" or "overdue-restricted";
    public static string EmailSubject(BillingReminder record) => record.Kind == "bill-issued"
        ? $"EduSmart - Monthly Tuition Bill - {record.MonthlyInvoice.Month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} - {record.MonthlyInvoice.Number}"
        : $"EduSmart - Overdue Monthly Bill - {record.MonthlyInvoice.Number}";
    public static string EmailBody(BillingReminder record, string? publicBaseUrl)
    {
        var bill = record.MonthlyInvoice;
        var month = bill.Month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var amount = bill.Total.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string intro = record.Kind == "bill-issued" ? "Your monthly tuition bill is ready." : "Your monthly tuition bill remains unpaid.";
        string body = $"{intro}\n\nBill: {bill.Number}\nTuition month: {month}\nAmount: RM {amount}\nDue: {bill.DueAtUtc.AddHours(8).AddSeconds(-1):dd MMM yyyy HH:mm} (Malaysia time)\n\nSign in to EduSmart and open Monthly Tuition Bills to review and pay.";
        // Use the configured site origin, never a request Host header or payment/session token.
        if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(url.UserInfo) && string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment))
            body += $"\n{url.GetLeftPart(UriPartial.Authority)}/MonthlyBilling/Details/{bill.MonthlyInvoiceId}";
        return body + "\n\nIf you have already paid, check the latest status on the website. Contact the centre if you need help.";
    }
    public static string ReminderKind(MonthlyInvoice bill, DateTime nowUtc)
    {
        if (bill.Status != MonthlyInvoiceStatus.Pending || bill.Total <= 0) return "";
        var days = nowUtc < bill.DueAtUtc ? 0 : (nowUtc.AddHours(8).Date - bill.DueAtUtc.AddHours(8).Date).Days + 1;
        return days > 7 ? "overdue-restricted" : days >= 4 ? "overdue-warning" : "bill-issued";
    }
    private async Task NotifyAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var day = MonthlyBillingService.MalaysiaToday;
        var ids = await db.Set<MonthlyInvoice>().Where(x => x.Status == MonthlyInvoiceStatus.Pending && x.Total > 0)
            .Select(x => x.MonthlyInvoiceId).ToListAsync(ct);
        foreach (var invoiceId in ids)
        {
            var bill = await db.Set<MonthlyInvoice>().AsNoTracking().SingleAsync(x => x.MonthlyInvoiceId == invoiceId, ct);
            var kind = ReminderKind(bill, DateTime.UtcNow);
            if (kind.Length == 0) continue;
            var parents = await db.ParentStudents.Where(x => x.StudentProfileId == bill.StudentProfileId && x.IsActive)
                .Select(x => x.ParentProfile.ApplicationUserId).Distinct().ToListAsync(ct);
            var student = await db.StudentProfiles.Where(x => x.StudentProfileId == bill.StudentProfileId).Select(x => x.ApplicationUserId).SingleAsync(ct);
            var users = parents.Concat(new[] { student }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            foreach (var user in users)
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // A bill-issued notification is sent once. Overdue alerts at most once per day/type.
                bool exists = await db.Set<BillingReminder>().AnyAsync(x => x.MonthlyInvoiceId == invoiceId && x.UserId == user &&
                    x.Kind == kind && (kind == "bill-issued" || x.Day == day), ct);
                if (!exists)
                {
                    db.Add(new BillingReminder { MonthlyInvoiceId = invoiceId, UserId = user!, Kind = kind, Day = day });
                    var title = kind == "bill-issued" ? "Monthly tuition bill available" : "Monthly tuition payment overdue";
                    db.UserNotifications.Add(new UserNotification { UserId = user!, Title = title,
                        Text = $"{bill.Number}, {bill.Month:yyyy-MM}: RM {bill.Total:F2}. Open Payment & Billing > Monthly Tuition Bills." +
                            (kind == "overdue-restricted" ? " New enrollment and academic changes are restricted until payment is verified. Payment and support remain available." : "") });
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
        }
        // Verified active guardians receive a new-bill email once and restricted-overdue
        // emails at most once per day. Claim before SMTP to avoid duplicate uncertain sends.
        // No automatic retry of an attempted send: Sent means SMTP accepted, not inbox delivery.
        var settings = services.GetRequiredService<IOptions<EmailSettings>>().Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
        { logger.LogWarning("Billing reminder emails are not configured; in-app alerts remain available."); return; }
        var now = DateTime.UtcNow;
        var pending = await db.Set<BillingReminder>().AsNoTracking().Where(x =>
            ((x.Kind == "bill-issued" && x.MonthlyInvoice.DueAtUtc >= now) || (x.Kind == "overdue-restricted" && x.Day == day)) &&
            x.EmailAttemptedAtUtc == null && x.MonthlyInvoice.Status == MonthlyInvoiceStatus.Pending &&
            db.ParentStudents.Any(p => p.StudentProfileId == x.MonthlyInvoice.StudentProfileId && p.IsActive && p.ParentProfile.ApplicationUserId == x.UserId))
            .Select(x => x.BillingReminderId).ToListAsync(ct);
        foreach (var id in pending)
        {
            int claimed = await db.Set<BillingReminder>().Where(x => x.BillingReminderId == id && x.EmailAttemptedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EmailAttemptedAtUtc, DateTime.UtcNow), ct);
            if (claimed != 1) continue;
            db.ChangeTracker.Clear();
            var record = await db.Set<BillingReminder>().Include(x => x.MonthlyInvoice).SingleAsync(x => x.BillingReminderId == id, ct);
            if (record.MonthlyInvoice.Status != MonthlyInvoiceStatus.Pending ||
                !await db.ParentStudents.AnyAsync(p => p.StudentProfileId == record.MonthlyInvoice.StudentProfileId && p.IsActive && p.ParentProfile.ApplicationUserId == record.UserId, ct))
            { record.EmailError = "Not sent: bill is no longer unpaid or guardian link is inactive."; await db.SaveChangesAsync(ct); continue; }
            var account = await db.Users.AsNoTracking().SingleAsync(x => x.Id == record.UserId, ct);
            if (!account.IsActive || !account.EmailConfirmed || string.IsNullOrWhiteSpace(account.Email)) { record.EmailError = "No verified guardian email."; await db.SaveChangesAsync(ct); continue; }
            try
            {
                using var smtp = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.UseSsl, UseDefaultCredentials = false, Timeout = 15000 };
                if (!string.IsNullOrEmpty(settings.UserName)) smtp.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                using var message = new MailMessage {
                    From = new MailAddress(settings.FromAddress, settings.FromName),
                    Subject = EmailSubject(record),
                    Body = EmailBody(record, services.GetRequiredService<IConfiguration>()["App:PublicBaseUrl"]),
                    IsBodyHtml = false };
                message.To.Add(new MailAddress(account.Email));
                using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                sendTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                await smtp.SendMailAsync(message, sendTimeout.Token); record.EmailSentAtUtc = DateTime.UtcNow;
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            { record.EmailError = "Email delivery was not confirmed. Check SMTP before retrying."; logger.LogError(e, "Billing email delivery unconfirmed for reminder {Id}", id); }
            await db.SaveChangesAsync(ct);
        }
    }
}
