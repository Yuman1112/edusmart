using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;

namespace SecureSmartTuitionSystem.BackgroundServices;

public class NotificationOptions
{
    public int AssignmentReminderDaysBefore { get; set; } = 3;
}

/// <summary>
/// Communication & Announcement Module — Table 4.17 "Assignment Reminder
/// Notifications" and Table 4.18 "Deadline &amp; Late Submission Alerts".
/// Assignment/AssignmentSubmission are [STUB] tables owned by the Learning
/// &amp; Assessment Module; this service only reads them to decide who to
/// notify, and never writes to them. "New materials uploaded" / "assessment
/// graded" event-based triggers (Functional Requirements) are exposed as
/// public methods below for that module's own code to call directly the
/// moment the event happens, since a polling loop is the wrong tool for an
/// instantaneous event trigger.
/// </summary>
public class NotificationSchedulerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationSchedulerService> _logger;
    private readonly NotificationOptions _options;

    public NotificationSchedulerService(IServiceProvider services, ILogger<NotificationSchedulerService> logger, IOptions<NotificationOptions> options)
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
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await SendUpcomingDeadlineRemindersAsync(db, notifications);
                await SendOverdueSubmissionAlertsAsync(db, notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationSchedulerService cycle failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    /// <summary>Table 4.17: reminder starting N days (default 3) before an assignment deadline.</summary>
    private async Task SendUpcomingDeadlineRemindersAsync(AppDbContext db, INotificationService notifications)
    {
        var now = DateTime.UtcNow;
        var reminderWindowEnd = now.AddDays(_options.AssignmentReminderDaysBefore);

        var dueSoonAssignments = await db.Assignments
            .Where(a => a.DueDate > now && a.DueDate <= reminderWindowEnd)
            .Include(a => a.ClassSession)
            .ToListAsync();

        if (dueSoonAssignments.Count == 0) return; // A1: "No upcoming assignment deadlines scheduled for reminders."

        foreach (var assignment in dueSoonAssignments)
        {
            var enrolledStudents = await db.Enrollments
                .Where(e => e.ClassSessionId == assignment.ClassSessionId && e.Status == EnrollmentStatus.Approved)
                .Include(e => e.Student)
                .Select(e => e.Student!)
                .ToListAsync();

            foreach (var student in enrolledStudents)
            {
                var alreadySubmitted = await db.AssignmentSubmissions.AnyAsync(s =>
                    s.AssignmentId == assignment.AssignmentId && s.StudentId == student.StudentId && s.SubmittedAt != null);

                if (alreadySubmitted) continue;

                if (student.UserId is not null)
                {
                    await notifications.SendOncePerDayAsync(student.UserId, NotificationType.AssignmentReminder,
                        "Assignment due soon", $"\"{assignment.Title}\" is due on {assignment.DueDate:dd MMM yyyy}.", assignment.AssignmentId);
                }

                if (student.Guardian?.UserId is not null)
                {
                    await notifications.SendOncePerDayAsync(student.Guardian.UserId, NotificationType.AssignmentReminder,
                        "Assignment due soon", $"{student.FullName}'s \"{assignment.Title}\" is due on {assignment.DueDate:dd MMM yyyy}.", assignment.AssignmentId);
                }
            }
        }
    }

    /// <summary>Table 4.18: overdue alert once a deadline has passed with no submission.</summary>
    private async Task SendOverdueSubmissionAlertsAsync(AppDbContext db, INotificationService notifications)
    {
        var now = DateTime.UtcNow;

        var overdueAssignments = await db.Assignments
            .Where(a => a.DueDate < now)
            .ToListAsync();

        if (overdueAssignments.Count == 0) return; // A1: "All submissions up-to-date. No alerts needed."

        foreach (var assignment in overdueAssignments)
        {
            var enrolledStudents = await db.Enrollments
                .Where(e => e.ClassSessionId == assignment.ClassSessionId && e.Status == EnrollmentStatus.Approved)
                .Include(e => e.Student).ThenInclude(s => s!.Guardian)
                .Select(e => e.Student!)
                .ToListAsync();

            foreach (var student in enrolledStudents)
            {
                var submitted = await db.AssignmentSubmissions.AnyAsync(s =>
                    s.AssignmentId == assignment.AssignmentId && s.StudentId == student.StudentId && s.SubmittedAt != null);

                if (submitted) continue;

                // Functional Requirements: "disable further submission when a deadline has
                // passed, unless late submission has been enabled" — enforced by the Learning
                // & Assessment Module's own submission endpoint checking assignment.LateSubmissionAllowed;
                // this service is only responsible for the alert.
                var message = assignment.LateSubmissionAllowed
                    ? $"\"{assignment.Title}\" is now overdue. Late submission is still open."
                    : $"\"{assignment.Title}\" is now overdue and submissions are closed.";

                if (student.UserId is not null)
                    await notifications.SendOncePerDayAsync(student.UserId, NotificationType.AssignmentOverdue, "Assignment overdue", message, assignment.AssignmentId);

                if (student.Guardian?.UserId is not null)
                    await notifications.SendOncePerDayAsync(student.Guardian.UserId, NotificationType.AssignmentOverdue, "Assignment overdue", message, assignment.AssignmentId);
            }
        }
    }

    /// <summary>
    /// Event-based trigger (Functional Requirements): call this the instant a
    /// tutor grades a submission. Exposed statically-callable via DI from the
    /// Learning & Assessment Module's own controller/service.
    /// </summary>
    public static async Task NotifyGradedAsync(AppDbContext db, INotificationService notifications, int submissionId)
    {
        var submission = await db.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Include(s => s.Student).ThenInclude(s => s.Guardian)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId);

        if (submission is null) return;

        var message = $"\"{submission.Assignment.Title}\" has been graded: {submission.Score:F1}.";

        if (submission.Student.UserId is not null)
            await notifications.SendOncePerDayAsync(submission.Student.UserId, NotificationType.AssignmentGraded, "Assignment graded", message, submission.AssignmentId);

        if (submission.Student.Guardian?.UserId is not null)
            await notifications.SendOncePerDayAsync(submission.Student.Guardian.UserId, NotificationType.AssignmentGraded, "Assignment graded", message, submission.AssignmentId);
    }

    /// <summary>
    /// Event-based trigger (Functional Requirements): call this the instant
    /// new learning materials are uploaded (Learning & Assessment Module
    /// integration point — no Material entity exists in this report's scope).
    /// </summary>
    public static async Task NotifyMaterialUploadedAsync(AppDbContext db, INotificationService notifications, int classSessionId, string materialTitle)
    {
        var studentUserIds = await db.Enrollments
            .Where(e => e.ClassSessionId == classSessionId && e.Status == EnrollmentStatus.Approved)
            .Include(e => e.Student)
            .Select(e => e.Student!.UserId)
            .Where(id => id != null)
            .ToListAsync();

        foreach (var userId in studentUserIds)
        {
            await notifications.SendOncePerDayAsync(userId!, NotificationType.MaterialUploaded,
                "New learning material", $"New material \"{materialTitle}\" has been uploaded.", classSessionId);
        }
    }
}
