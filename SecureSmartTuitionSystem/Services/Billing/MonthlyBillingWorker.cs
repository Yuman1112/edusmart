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
                await scope.ServiceProvider.GetRequiredService<MonthlyBillingService>().GenerateCurrentAsync(stoppingToken);
                await NotifyAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "Monthly billing/notifications failed; next run will retry safely."); }
            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
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
        // Only guardians receive the >7-day email. The attempt is durably claimed
        // before SMTP; ambiguous sends are not automatically retried the same day.
        var settings = services.GetRequiredService<IOptions<EmailSettings>>().Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
        { logger.LogWarning("Billing reminder emails are not configured; in-app alerts remain available."); return; }
        var pending = await db.Set<BillingReminder>().AsNoTracking().Where(x => x.Kind == "overdue-restricted" && x.Day == day &&
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
            var account = await db.Users.AsNoTracking().SingleAsync(x => x.Id == record.UserId, ct);
            if (!account.EmailConfirmed || string.IsNullOrWhiteSpace(account.Email)) { record.EmailError = "No verified guardian email."; await db.SaveChangesAsync(ct); continue; }
            try
            {
                using var smtp = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.UseSsl };
                if (!string.IsNullOrEmpty(settings.UserName)) smtp.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                using var message = new MailMessage(settings.FromAddress, account.Email) {
                    Subject = $"EduSmart overdue monthly bill {record.MonthlyInvoice.Number}",
                    Body = $"Monthly tuition for {record.MonthlyInvoice.Month:yyyy-MM} remains unpaid. Please sign in to EduSmart and open Monthly Tuition Bills to review and pay. Contact the centre if you need help." };
                await smtp.SendMailAsync(message, ct); record.EmailSentAtUtc = DateTime.UtcNow;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            { record.EmailError = "Email delivery was not confirmed. Check SMTP before retrying."; logger.LogError(e, "Billing email delivery unconfirmed for reminder {Id}", id); }
            await db.SaveChangesAsync(ct);
        }
    }
}
