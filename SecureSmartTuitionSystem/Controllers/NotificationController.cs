using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Filters;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.ViewModels;

namespace SecureSmartTuitionSystem.Controllers;

/// <summary>
/// Communication & Announcement Module — notification read/unread tracking
/// (Table 4.19) and announcement broadcast (Functional Requirements:
/// "broadcast announcements to parents and track their delivery and read
/// status"). Deadline/overdue/grading triggers live in
/// BackgroundServices/NotificationSchedulerService.
/// </summary>
[Authorize]
public class NotificationController : Controller
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public NotificationController(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    /// <summary>Table 4.19, step 2-3.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(bool? unreadOnly)
    {
        var userId = this.CurrentUserId()!;
        var results = await _notifications.GetForUserAsync(userId, unreadOnly);

        // A1: empty list -> "You have no notifications at this time."
        return Json(results.Select(n => new { n.NotificationId, n.NotificationType, n.Title, n.Message, n.IsRead, n.CreatedAt }));
    }

    /// <summary>Table 4.19 "Mark as read" / "Mark as unread".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRead(int notificationId, bool isRead)
    {
        var userId = this.CurrentUserId()!;
        await _notifications.SetReadStatusAsync(notificationId, userId, isRead);
        return Ok(new { message = isRead ? "Notification marked as read." : "Notification marked as unread." });
    }

    /// <summary>Table 4.19 "Mark all as read".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = this.CurrentUserId()!;
        await _notifications.MarkAllReadAsync(userId);
        return Ok(new { message = "All notifications marked as read." });
    }

    /// <summary>Functional Requirements: broadcast to parents; per-recipient delivery/read tracking.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [Audit("Notification.BroadcastAnnouncement", "Announcement")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastAnnouncementRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var recipientIds = request.RecipientUserIds.Count > 0
            ? request.RecipientUserIds
            : await _db.Users.Where(u => u.Role == UserRole.Guardian).Select(u => u.Id).ToListAsync();

        var announcement = new Announcement
        {
            Title = request.Title,
            Body = request.Body,
            PostedByUserId = this.CurrentUserId()!
        };

        foreach (var recipientId in recipientIds)
        {
            announcement.Recipients.Add(new AnnouncementRecipient
            {
                UserId = recipientId,
                DeliveredAt = DateTime.UtcNow
            });
        }

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync();

        foreach (var recipientId in recipientIds)
        {
            await _notifications.SendOncePerDayAsync(recipientId, NotificationType.AnnouncementPosted,
                request.Title, request.Body, announcement.AnnouncementId);
        }

        return Ok(new { announcement.AnnouncementId, RecipientCount = recipientIds.Count });
    }

    /// <summary>Marks an announcement as read by the current user (drives the delivery/read tracking Admins see).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAnnouncementRead(int announcementId)
    {
        var userId = this.CurrentUserId()!;
        var recipient = await _db.AnnouncementRecipients.FirstOrDefaultAsync(r =>
            r.AnnouncementId == announcementId && r.UserId == userId);

        if (recipient is null) return NotFound();

        recipient.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Admin view of an announcement's delivery/read status across all recipients.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AnnouncementStatus(int announcementId)
    {
        var recipients = await _db.AnnouncementRecipients
            .Where(r => r.AnnouncementId == announcementId)
            .Select(r => new { r.UserId, r.DeliveredAt, r.ReadAt })
            .ToListAsync();

        return Json(recipients);
    }
}
