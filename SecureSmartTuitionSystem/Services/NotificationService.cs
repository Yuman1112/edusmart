using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> SendOncePerDayAsync(string userId, NotificationType type, string title, string message, int? referenceId = null)
    {
        var todayUtc = DateTime.UtcNow.Date;

        // Once-per-day-per-type throttle (Functional Requirements: Communication and Announcement;
        // Ch.2.3 "Rule-Based Notification Triggering": "a once-per-day trigger rule being applied").
        var alreadySentToday = await _db.Notifications.AnyAsync(n =>
            n.UserId == userId &&
            n.NotificationType == type &&
            n.CreatedAt >= todayUtc);

        if (alreadySentToday)
        {
            return false;
        }

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            NotificationType = type,
            ReferenceId = referenceId,
            Title = title,
            Message = message
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SetReadStatusAsync(int notificationId, string userId, bool isRead)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        if (notification is null) return; // silently no-op: avoids leaking existence of other users' notifications

        notification.IsRead = isRead;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task<List<Notification>> GetForUserAsync(string userId, bool? unreadOnly = null)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }
}
