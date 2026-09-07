using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

/// <summary>
/// Rule-based notification triggering (Ch.2.3 "Rule-Based Notification
/// Triggering", Communication &amp; Announcement Module). Every alert type is
/// throttled to at most one per user per day (Functional Requirements:
/// "display each reminder or notification type no more than once per day to
/// the same user").
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends <paramref name="type"/> to <paramref name="userId"/> unless one
    /// of the same type was already sent to that user today. Returns false
    /// (and sends nothing) when throttled.
    /// </summary>
    Task<bool> SendOncePerDayAsync(string userId, NotificationType type, string title, string message, int? referenceId = null);

    /// <summary>Marks one notification as read/unread (Table 4.19).</summary>
    Task SetReadStatusAsync(int notificationId, string userId, bool isRead);

    /// <summary>"Mark all as read" (Table 4.19).</summary>
    Task MarkAllReadAsync(string userId);

    Task<List<Notification>> GetForUserAsync(string userId, bool? unreadOnly = null);
}
