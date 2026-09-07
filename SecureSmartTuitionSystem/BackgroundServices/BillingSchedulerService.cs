using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;

namespace SecureSmartTuitionSystem.BackgroundServices;

public class BillingOptions
{
    public int GracePeriodWarningStartDay { get; set; } = 4;
    public int GracePeriodFinalDay { get; set; } = 7;
}

/// <summary>
/// Payment & Billing Module automation that has no single human "actor" in
/// the use case tables because it runs on a schedule:
///   - generates each student's monthly bill on the 1st of every month
///     (Functional Requirements: "automatically generate a monthly bill...
///     on the first day of each month");
///   - applies the 4-7 day warning / &gt;7 day final-warning-plus-email /
///     account suspension grace-period rules;
///   - reactivates a suspended account once its outstanding balance clears.
/// Runs hourly and is idempotent (checks for an existing invoice/state
/// before acting), so re-running after a restart is always safe.
/// </summary>
public class BillingSchedulerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BillingSchedulerService> _logger;
    private readonly BillingOptions _options;

    public BillingSchedulerService(IServiceProvider services, ILogger<BillingSchedulerService> logger, IOptions<BillingOptions> options)
    {
        _services = services;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var feeCalc = scope.ServiceProvider.GetRequiredService<IFeeCalculationService>();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await GenerateMonthlyBillsAsync(db, feeCalc);
                await ApplyOverdueRulesAsync(db, notifications);
                await ReactivateClearedAccountsAsync(db, notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BillingSchedulerService cycle failed.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task GenerateMonthlyBillsAsync(AppDbContext db, IFeeCalculationService feeCalc)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today.Day != 1) return; // "on the first day of each month"

        var billingMonth = new DateOnly(today.Year, today.Month, 1);

        var activeStudents = await db.Students
            .Where(s => db.Enrollments.Any(e => e.StudentId == s.StudentId && e.Status == EnrollmentStatus.Approved))
            .ToListAsync();

        foreach (var student in activeStudents)
        {
            var alreadyBilled = await db.Invoices.AnyAsync(i =>
                i.StudentId == student.StudentId && i.BillingMonth == billingMonth && i.InvoiceType == InvoiceType.Monthly);

            if (alreadyBilled) continue; // idempotent re-run guard

            var invoice = await feeCalc.BuildMonthlyInvoiceAsync(student, billingMonth);
            if (invoice.TotalAmount <= 0) continue; // no billable subjects this month

            db.Invoices.Add(invoice);
        }

        await db.SaveChangesAsync();
    }

    private async Task ApplyOverdueRulesAsync(AppDbContext db, INotificationService notifications)
    {
        var overdueInvoices = await db.Invoices
            .Include(i => i.Student).ThenInclude(s => s.User)
            .Include(i => i.Student).ThenInclude(s => s.Guardian).ThenInclude(g => g!.User)
            .Where(i => i.Status == InvoiceStatus.PendingPayment)
            .ToListAsync();

        foreach (var invoice in overdueInvoices)
        {
            var daysOverdue = invoice.DaysOverdue;
            if (daysOverdue <= 0) continue;

            if (daysOverdue > _options.GracePeriodFinalDay)
            {
                invoice.Status = InvoiceStatus.Overdue;

                // Functional Requirements: "restrict a student or guardian account to
                // view-only access when payment remains overdue beyond the defined grace period."
                if (invoice.Student.User is not null) invoice.Student.User.IsActive = false;
                if (invoice.Student.Guardian?.User is not null) invoice.Student.Guardian.User.IsActive = false;

                if (invoice.Student.UserId is not null)
                {
                    // Functional Requirements: "send a warning notification and an email... beyond 7 days."
                    await notifications.SendOncePerDayAsync(invoice.Student.UserId, NotificationType.PaymentOverdueFinal,
                        "Payment overdue - account restricted",
                        $"Payment for {invoice.BillingMonth:MMMM yyyy} is more than {_options.GracePeriodFinalDay} days overdue. The account has been restricted to view-only access.",
                        invoice.InvoiceId);
                    // TODO integration point: also dispatch via the SMTP/email provider configured for this deployment.
                }
            }
            else if (daysOverdue >= _options.GracePeriodWarningStartDay)
            {
                // Functional Requirements: "display a warning notification... overdue by 4 to 7 days."
                if (invoice.Student.UserId is not null)
                {
                    await notifications.SendOncePerDayAsync(invoice.Student.UserId, NotificationType.PaymentOverdueWarning,
                        "Payment reminder",
                        $"Payment for {invoice.BillingMonth:MMMM yyyy} is {daysOverdue} day(s) overdue. Please settle it to avoid account restriction.",
                        invoice.InvoiceId);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task ReactivateClearedAccountsAsync(AppDbContext db, INotificationService notifications)
    {
        // Functional Requirements: "automatically reactivate a suspended account
        // once the outstanding payment has been received."
        var suspendedStudents = await db.Students
            .Include(s => s.User)
            .Include(s => s.Guardian).ThenInclude(g => g.User)
            .Where(s => s.User != null && !s.User.IsActive)
            .ToListAsync();

        foreach (var student in suspendedStudents)
        {
            var hasOutstanding = await db.Invoices.AnyAsync(i =>
                i.StudentId == student.StudentId &&
                (i.Status == InvoiceStatus.PendingPayment || i.Status == InvoiceStatus.Overdue));

            if (hasOutstanding) continue;

            student.User!.IsActive = true;
            if (student.Guardian?.User is not null) student.Guardian.User.IsActive = true;

            if (student.UserId is not null)
            {
                await notifications.SendOncePerDayAsync(student.UserId, NotificationType.AccountReactivated,
                    "Account reactivated", "Your outstanding balance has been cleared and full account access has been restored.");
            }
        }

        await db.SaveChangesAsync();
    }
}
