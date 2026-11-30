using SecureSmartTuitionSystem.Services.Tables;
using Microsoft.AspNetCore.Authorization;
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
    private readonly SecurityExplanationAI _explanations;
    public SecurityController(ApplicationDbContext db, AuditLogIntegrity integrity,
        IOptions<SecuritySettings> settings, IOptions<EmailSettings> email, SecurityExplanationAI explanations)
    { _db = db; _integrity = integrity; _settings = settings.Value; _email = email.Value; _explanations = explanations; }

    [HttpGet]
    public async Task<IActionResult> Details(long id, string? returnUrl, CancellationToken ct)
    {
        var model = await LoadDetailsAsync(id, returnUrl, ct);
        return model == null ? NotFound() : View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("security-explanation")]
    public async Task<IActionResult> Explain(long id, string? returnUrl, CancellationToken ct)
    {
        var model = await LoadDetailsAsync(id, returnUrl, ct);
        if (model == null) return NotFound();
        // An integrity failure must not be hidden behind a generated explanation.
        if (!model.IntegrityValid) { model.AIExplanation = "Integrity verification failed. Review the original record."; return View("Details", model); }
        var result = await _explanations.ExplainAsync(model.Entry, ct);
        model.AIExplanation = result.Text; model.GeneratedByAI = result.GeneratedByAI;
        return View("Details", model);
    }
    private async Task<SecurityDetailsViewModel?> LoadDetailsAsync(long id, string? returnUrl, CancellationToken ct)
    {
        var entry = await _db.AuditLogEntries.AsNoTracking().SingleOrDefaultAsync(x => x.AuditLogEntryId == id, ct);
        if (entry == null) return null;
        return new SecurityDetailsViewModel {
            Entry = entry, IntegrityValid = _integrity.Verify(entry), Summary = SecurityActivityText.Summary(entry),
            AIConfigured = _explanations.IsConfigured, ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        };
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? userId,
        SecuritySeverity? severity, int pageNumber = 1)
    {
        var query = _db.AuditLogEntries.AsNoTracking();
        if (from.HasValue)
        { var start = from.Value.Date == DateTime.MinValue.Date ? DateTime.MinValue : from.Value.Date.AddHours(-8); query = query.Where(x => x.OccurredAtUtc >= start); }
        if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
        { var end = to.Value.Date.AddDays(1).AddHours(-8); query = query.Where(x => x.OccurredAtUtc < end); }
        userId = SecurityEventService.Limit((userId ?? string.Empty).Trim(), 450);
        if (userId.Length > 0) query = query.Where(x => (x.ActorUserId != null && x.ActorUserId.Contains(userId)) || (x.ActorName != null && x.ActorName.Contains(userId)));
        if (severity.HasValue) query = query.Where(x => x.Severity == severity.Value);
        int total = await query.CountAsync();
        int pages = Math.Max(1, (int)Math.Ceiling(total / 25d));
        pageNumber = Math.Clamp(pageNumber, 1, pages);
        List<SecureSmartTuitionSystem.Models.Security.AuditLogEntry> records;
        var sort = HttpContext?.Request.Query["tableSort"].ToString();
        if (sort is "activity" or "check" or "user") {
            var entries = await query.ToListAsync();
            var ordered = TableOrder.Local(entries, HttpContext?.Request.Query, "time", true,
                ("time", x => x.OccurredAtUtc), ("activity", x => SecurityActivityText.Summary(x)),
                ("check", x => _integrity.Verify(x) ? "Verified" : "Review required"), ("user", x => SecurityActivityText.UserLabel(x.ActorName)));
            records = ordered.ThenByDescending(x => x.AuditLogEntryId).Skip((pageNumber - 1) * 25).Take(25).ToList();
        } else {
            records = await TableOrder.Apply(query, HttpContext?.Request.Query, "time", true,
                ("time", x => x.OccurredAtUtc), ("severity", x => x.Severity))
                .ThenByDescending(x => x.AuditLogEntryId).Skip((pageNumber - 1) * 25).Take(25).ToListAsync();
        }
        var since = DateTime.UtcNow.AddDays(-1);
        return View(new SecurityIndexViewModel {
            From = from, To = to, UserId = userId, Severity = severity,
            PageNumber = pageNumber, TotalPages = pages, TotalRecords = total,
            RecentAlerts = await _db.AuditLogEntries.CountAsync(x =>
                x.OccurredAtUtc >= since && (int)x.Severity >= (int)SecuritySeverity.High),
            EmailAlertsConfigured = !string.IsNullOrWhiteSpace(_settings.AdminAlertEmail) &&
                !string.IsNullOrWhiteSpace(_email.Host) && !string.IsNullOrWhiteSpace(_email.FromAddress),
            Rows = records.Select(x => new SecurityLogRow { Entry = x, IntegrityValid = _integrity.Verify(x) }).ToList()
        });
    }
}
