using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Settings;
using SecureSmartTuitionSystem.ViewModels.Security;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SecurityController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditLogIntegrity _integrity;
    private readonly SecuritySettings _settings;
    private readonly EmailSettings _email;
    public SecurityController(ApplicationDbContext db, AuditLogIntegrity integrity,
        IOptions<SecuritySettings> settings, IOptions<EmailSettings> email)
    { _db = db; _integrity = integrity; _settings = settings.Value; _email = email.Value; }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EncryptLegacyFinancialNotes()
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var paymentIds = await _db.Database.SqlQueryRaw<int>(
            "SELECT TOP (100) [PaymentId] AS [Value] FROM [Payments] WHERE [FailureReason] IS NOT NULL AND [FailureReason] NOT LIKE @prefix ORDER BY [PaymentId]",
            new SqlParameter("@prefix", "edusmart:v1:%")).ToListAsync();
        var billIds = await _db.Database.SqlQueryRaw<int>(
            "SELECT TOP (100) [BillId] AS [Value] FROM [Bills] WHERE [CancellationReason] IS NOT NULL AND [CancellationReason] NOT LIKE @prefix ORDER BY [BillId]",
            new SqlParameter("@prefix", "edusmart:v1:%")).ToListAsync();
        var payments = await _db.Payments.Where(x => paymentIds.Contains(x.PaymentId)).ToListAsync();
        var bills = await _db.Bills.Where(x => billIds.Contains(x.BillId)).ToListAsync();
        foreach (var payment in payments) _db.Entry(payment).Property(x => x.FailureReason).IsModified = true;
        foreach (var bill in bills) _db.Entry(bill).Property(x => x.CancellationReason).IsModified = true;
        if (payments.Count + bills.Count > 0) await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["SecurityNotice"] = $"Encrypted {payments.Count + bills.Count} existing financial note(s). Repeat until the count is zero.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? userId,
        string? eventName, SecuritySeverity? severity, int pageNumber = 1)
    {
        var query = _db.AuditLogEntries.AsNoTracking();
        if (from.HasValue)
        { var start = from.Value.Date == DateTime.MinValue.Date ? DateTime.MinValue : from.Value.Date.AddHours(-8); query = query.Where(x => x.OccurredAtUtc >= start); }
        if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
        { var end = to.Value.Date.AddDays(1).AddHours(-8); query = query.Where(x => x.OccurredAtUtc < end); }
        userId = SecurityEventService.Limit((userId ?? string.Empty).Trim(), 450);
        eventName = SecurityEventService.Limit((eventName ?? string.Empty).Trim(), 100);
        if (userId.Length > 0) query = query.Where(x => (x.ActorUserId != null && x.ActorUserId.Contains(userId)) || (x.ActorName != null && x.ActorName.Contains(userId)));
        if (eventName.Length > 0) query = query.Where(x => x.EventName.Contains(eventName));
        if (severity.HasValue) query = query.Where(x => x.Severity == severity.Value);
        int total = await query.CountAsync();
        int pages = Math.Max(1, (int)Math.Ceiling(total / 25d));
        pageNumber = Math.Clamp(pageNumber, 1, pages);
        var records = await query.OrderByDescending(x => x.AuditLogEntryId)
            .Skip((pageNumber - 1) * 25).Take(25).ToListAsync();
        var since = DateTime.UtcNow.AddDays(-1);
        return View(new SecurityIndexViewModel {
            From = from, To = to, UserId = userId, EventName = eventName, Severity = severity,
            PageNumber = pageNumber, TotalPages = pages, TotalRecords = total,
            RecentAlerts = await _db.AuditLogEntries.CountAsync(x =>
                x.OccurredAtUtc >= since && (int)x.Severity >= (int)SecuritySeverity.High),
            EmailAlertsConfigured = !string.IsNullOrWhiteSpace(_settings.AdminAlertEmail) &&
                !string.IsNullOrWhiteSpace(_email.Host) && !string.IsNullOrWhiteSpace(_email.FromAddress),
            Rows = records.Select(x => new SecurityLogRow { Entry = x, IntegrityValid = _integrity.Verify(x) }).ToList()
        });
    }
}
