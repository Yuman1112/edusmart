using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
namespace SecureSmartTuitionSystem.Services.Communication;
public sealed class EnrollmentNoticeWorker(IServiceScopeFactory scopes, ILogger<EnrollmentNoticeWorker> logger) : BackgroundService
{
    public static async Task ProcessAsync(ApplicationDbContext db, IMessageProtectionService protection,
        IEnrollmentNoticeSender sender, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        await db.Set<EnrollmentNotice>().Where(x => x.State == EnrollmentNoticeState.Sending && x.AttemptedAtUtc < cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Unconfirmed)
                .SetProperty(x => x.Note, "Sending was interrupted. Check the mailbox/mail logs before recovery."), ct);
        if (!sender.IsConfigured) return;
        var ids = await db.Set<EnrollmentNotice>().AsNoTracking().Where(x => x.State == EnrollmentNoticeState.Pending)
            .OrderBy(x => x.EnrollmentNoticeId).Take(20).Select(x => x.EnrollmentNoticeId).ToListAsync(ct);
        foreach (var id in ids)
        {
            var attempt = Guid.NewGuid();
            if (await db.Set<EnrollmentNotice>().Where(x => x.EnrollmentNoticeId == id && x.State == EnrollmentNoticeState.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Sending).SetProperty(x => x.AttemptId, attempt)
                    .SetProperty(x => x.AttemptedAtUtc, DateTime.UtcNow).SetProperty(x => x.Note, (string?)null), ct) != 1) continue;
            var row = await db.Set<EnrollmentNotice>().AsNoTracking().SingleAsync(x => x.EnrollmentNoticeId == id, ct);
            EnrollmentNoticePayload payload;
            try { payload = JsonSerializer.Deserialize<EnrollmentNoticePayload>(protection.UnprotectText(row.ProtectedPayload)) ?? throw new JsonException(); }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
            {
                await db.Set<EnrollmentNotice>().Where(x => x.EnrollmentNoticeId == id && x.AttemptId == attempt)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Failed)
                        .SetProperty(x => x.Note, "Cannot read email payload. Check Data Protection keys before retrying."), ct);
                continue;
            }
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await sender.SendAsync(payload, row.Kind is "Rejected" or "Cancelled", timeout.Token);
                await db.Set<EnrollmentNotice>().Where(x => x.EnrollmentNoticeId == id && x.AttemptId == attempt)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Sent)
                        .SetProperty(x => x.SentAtUtc, DateTime.UtcNow).SetProperty(x => x.Note, "Accepted for sending; inbox delivery is not verified."), ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // SMTP exceptions can happen after acceptance. Never automatically resend an uncertain attempt.
                await db.Set<EnrollmentNotice>().Where(x => x.EnrollmentNoticeId == id && x.AttemptId == attempt)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Unconfirmed)
                        .SetProperty(x => x.Note, "Sending could not be confirmed. Check the mailbox/mail logs before recovery."), ct);
            }
        }
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var services = scope.ServiceProvider;
                await ProcessAsync(services.GetRequiredService<ApplicationDbContext>(), services.GetRequiredService<IMessageProtectionService>(),
                    services.GetRequiredService<IEnrollmentNoticeSender>(), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Enrollment email processing failed; pending notices are retained for recovery."); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
