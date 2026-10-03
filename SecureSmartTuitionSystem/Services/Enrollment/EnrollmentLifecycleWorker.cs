using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class EnrollmentLifecycleWorker(IServiceScopeFactory scopes, ILogger<EnrollmentLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var today = ClassScheduleService.Today;
                var due = await db.StudentEnrollments.Where(x => x.Status == StudentEnrollmentStatus.PendingDrop && x.DropEffectiveDate <= today).Take(200).ToListAsync(stoppingToken);
                foreach (var e in due) { e.Status = StudentEnrollmentStatus.Dropped; e.EndedAt = e.DropEffectiveDate; }
                var finished = await db.StudentEnrollments.Include(x => x.ClassSession).Where(x =>
                    (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop) &&
                    (x.StartDate.Year < today.Year || (x.ClassSession.EndDate.HasValue && x.ClassSession.EndDate < today))).Take(200).ToListAsync(stoppingToken);
                foreach (var e in finished) { e.Status = StudentEnrollmentStatus.Completed; var cutoff = AcademicYearPolicy.EndOfYear(e.StartDate).AddDays(1); if (e.ClassSession.EndDate.HasValue && e.ClassSession.EndDate.Value.AddDays(1) < cutoff) cutoff = e.ClassSession.EndDate.Value.AddDays(1); e.EndedAt = cutoff; }
                if (due.Count + finished.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Enrollment lifecycle update failed."); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
