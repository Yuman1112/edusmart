using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class EnrollmentLifecycleWorker(IServiceScopeFactory scopes, ILogger<EnrollmentLifecycleWorker> logger) : BackgroundService
{
    // Persist due withdrawals before selecting completions, within one transaction.
    // A due withdrawal wins if both events are observed in the same run.
    public static async Task<int> UpdateAsync(ApplicationDbContext db, DateTime date, CancellationToken ct = default)
    {
        var today = date.Date;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var due = await db.StudentEnrollments.Where(x => x.Status == StudentEnrollmentStatus.PendingDrop && x.DropEffectiveDate <= today)
            .OrderBy(x => x.StudentEnrollmentId).Take(200).ToListAsync(ct);
        foreach (var e in due) { e.Status = StudentEnrollmentStatus.Dropped; e.EndedAt = e.DropEffectiveDate; }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        var finished = await db.StudentEnrollments.Include(x => x.ClassSession).Where(x =>
            (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop) &&
            (!x.DropEffectiveDate.HasValue || x.DropEffectiveDate > today) &&
            (x.StartDate.Year < today.Year || (x.ClassSession.EndDate.HasValue && x.ClassSession.EndDate < today)))
            .OrderBy(x => x.StudentEnrollmentId).Take(200).ToListAsync(ct);
        foreach (var e in finished) {
            e.Status = StudentEnrollmentStatus.Completed;
            var cutoff = AcademicYearPolicy.EndOfYear(e.StartDate).AddDays(1);
            if (e.ClassSession.EndDate.HasValue && e.ClassSession.EndDate.Value.Date.AddDays(1) < cutoff)
                cutoff = e.ClassSession.EndDate.Value.Date.AddDays(1);
            e.EndedAt = cutoff;
        }
        if (finished.Count > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return due.Count + finished.Count;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await UpdateAsync(db, ClassScheduleService.Today, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Enrollment lifecycle update failed."); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
