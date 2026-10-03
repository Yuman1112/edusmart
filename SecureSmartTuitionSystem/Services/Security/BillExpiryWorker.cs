using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
namespace SecureSmartTuitionSystem.Services.Security;
public sealed class BillExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BillExpiryWorker> _logger;
    public BillExpiryWorker(IServiceScopeFactory scopes, ILogger<BillExpiryWorker> logger)
    { _scopes = scopes; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await using var transaction = await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, stoppingToken);
                var now = DateTime.UtcNow;
                var bills = await db.Bills.Where(x => x.Status == BillStatus.PendingPayment && x.DueAt < now)
                    .OrderBy(x => x.BillId).Take(200).ToListAsync(stoppingToken);
                foreach (var bill in bills) bill.Status = BillStatus.Overdue;
                if (bills.Count > 0) await db.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Overdue bill update failed; it will be retried."); }
        }
    }
}
