using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Services.Enrollment;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize]
public sealed class NotificationCenterController(ApplicationDbContext db) : Controller
{
    [HttpGet] public async Task<IActionResult> Index(int page = 1, bool unreadOnly = false)
    {
        page = Math.Clamp(page, 1, 10000);
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var today = ClassScheduleService.Today;
        var query = db.UserNotifications.AsNoTracking().Where(x => x.UserId == id && (x.AvailableFromDate == null || x.AvailableFromDate <= today));
        if (unreadOnly) query = query.Where(x => x.ReadAtUtc == null);
        int count = await query.CountAsync(); int pages = Math.Max(1, (int)Math.Ceiling(count / 25d)); page = Math.Clamp(page, 1, pages);
        ViewBag.Page = page; ViewBag.TotalPages = pages; ViewBag.TotalRecords = count; ViewBag.UnreadOnly = unreadOnly;
        return View(await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.UserNotificationId).Skip((page - 1) * 25).Take(25).ToListAsync());
    }
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> UnreadCounts()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var today = ClassScheduleService.Today;
        var now = DateTime.UtcNow;
        bool admin = User.IsInRole("Admin");
        int notifications = admin ? 0 : await db.UserNotifications.AsNoTracking().CountAsync(x => x.UserId == id && x.ReadAtUtc == null &&
            (x.AvailableFromDate == null || x.AvailableFromDate <= today));
        AnnouncementAudience? audience = User.IsInRole("Parent") ? AnnouncementAudience.Parent :
            User.IsInRole("Student") ? AnnouncementAudience.Student : User.IsInRole("Tutor") ? AnnouncementAudience.Tutor : null;
        int announcements = admin || !audience.HasValue ? 0 : await db.Announcements.AsNoTracking().CountAsync(x => !x.IsArchived && x.PublishAt <= now &&
            (!x.ExpiresAt.HasValue || x.ExpiresAt > now) && (x.TargetAudience == AnnouncementAudience.All || x.TargetAudience == audience.Value) &&
            !x.Reads.Any(r => r.UserId == id));
        int guestEnquiries = admin ? await db.GuestEnquiries.AsNoTracking().CountAsync(x => !x.IsResolved) : 0;
        return Json(new { announcements, notifications, guestEnquiries });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(int page = 1, bool unreadOnly = false)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var today = ClassScheduleService.Today;
        await db.UserNotifications.Where(x => x.UserId == id && x.ReadAtUtc == null &&
            (x.AvailableFromDate == null || x.AvailableFromDate <= today))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAtUtc, (DateTime?)DateTime.UtcNow));
        return RedirectToAction(nameof(Index), new { page, unreadOnly });
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Read(long id, int page = 1, bool unreadOnly = false)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var today = ClassScheduleService.Today;
        var n = await db.UserNotifications.FirstOrDefaultAsync(x => x.UserNotificationId == id && x.UserId == user && (x.AvailableFromDate == null || x.AvailableFromDate <= today));
        if (n == null) return NotFound();
        if (n.ReadAtUtc == null) { n.ReadAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { page, unreadOnly });
    }

}
