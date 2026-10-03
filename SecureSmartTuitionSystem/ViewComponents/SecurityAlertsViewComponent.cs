using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Security;
namespace SecureSmartTuitionSystem.ViewComponents;
public sealed class SecurityAlertsViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public SecurityAlertsViewComponent(ApplicationDbContext db) => _db = db;
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.IsInRole("Admin")) return Content(string.Empty);
        var since = DateTime.UtcNow.AddDays(-1);
        return View(await _db.AuditLogEntries.AsNoTracking().CountAsync(x =>
            x.OccurredAtUtc >= since && (int)x.Severity >= (int)SecuritySeverity.High));
    }
}
