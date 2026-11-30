using System.Text.Json;
using System.Text.Encodings.Web;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.ViewModels.Tutor;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorAccountStatusService(
    ApplicationDbContext db, UserManager<ApplicationUser> users,
    AuditLogIntegrity auditIntegrity)
{
    public async Task<List<string>> GetBlockingAssignmentsAsync(string userId)
    {
        var classes = await db.ClassSessions.AsNoTracking()
            .Where(c => c.TutorUserId == userId).ToListAsync();
        var changes = await db.ClassLessonChanges.AsNoTracking()
            .Where(c => c.ClassSession.TutorUserId == userId).ToListAsync();
        return FindBlockingAssignments(classes, changes, ClassScheduleService.Now);
    }

    // now is Malaysia local time, consistent with ClassScheduleService.Now.
    public static List<string> FindBlockingAssignments(
        IReadOnlyCollection<ClassSession> classes,
        IReadOnlyCollection<ClassLessonChange> changes,
        DateTime now)
    {
        var result = new List<string>();
        var codes = classes.ToDictionary(c => c.ClassSessionId, c => c.ClassCode);
        foreach (var c in classes.Where(c => c.Status == ClassSessionStatus.Active))
        {
            var first = ClassScheduleService.FirstDate(c);
            var from = first > now.Date ? first : now.Date;
            var next = from.AddDays(((int)c.DayOfWeek - (int)from.DayOfWeek + 7) % 7);
            if (next.Add(c.EndTime.ToTimeSpan()) <= now) next = next.AddDays(7);
            var cancelled = changes.Where(change => change.ClassSessionId == c.ClassSessionId)
                .Select(change => change.OriginalDate.Date).ToHashSet();
            var last = AcademicYearPolicy.ClassEnd(c);
            while (next <= last && (cancelled.Contains(next) || !LateMonthEnrollmentPolicy.IsRegularTeachingDate(next))) next = next.AddDays(7);
            if (next <= last)
                result.Add($"{c.ClassCode}: scheduled lesson {next:dd MMM yyyy}, {c.StartTime:HH:mm}–{c.EndTime:HH:mm}");
        }
        foreach (var change in changes)
        {
            string code = codes.GetValueOrDefault(change.ClassSessionId, "Class");
            if (!change.ReplacementDate.HasValue)
            {
                result.Add($"{code}: pending replacement for {change.OriginalDate:dd MMM yyyy}");
            }
            else if (!change.ReplacementEndTime.HasValue
                || change.ReplacementDate.Value.Date.Add(change.ReplacementEndTime.Value.ToTimeSpan()) > now)
            {
                result.Add($"{code}: replacement {change.ReplacementDate:dd MMM yyyy}");
            }
        }
        return result.Distinct().ToList();
    }

    public async Task<(bool Success, string Message)> ChangeAsync(
        string userId, bool activate, string actorUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(actorUserId) || userId == actorUserId)
            return (false, "You cannot change your own account status here.");

        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length == 0 || reason.Length > 500)
            return (false, "Enter a reason between 1 and 500 characters.");

        var actor = await users.FindByIdAsync(actorUserId);
        if (actor == null || !actor.IsActive || !await users.IsInRoleAsync(actor, "Admin"))
            return (false, "Only an active administrator can change tutor status.");

        await using var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var profile = await db.TutorProfiles.Include(p => p.ApplicationUser)
            .FirstOrDefaultAsync(p => p.ApplicationUserId == userId);
        if (profile == null)
            return (false, "Tutor account could not be found.");

        var user = profile.ApplicationUser;
        if (!await users.IsInRoleAsync(user, "Tutor")
            || await users.IsInRoleAsync(user, "Admin"))
            return (false, "Only tutor accounts without an Admin role can be changed here.");

        if (profile.IsActive == activate && user.IsActive == activate)
            return (true, activate ? "This tutor is already active." : "This tutor is already inactive.");

        if (!activate)
        {
            var blockers = await GetBlockingAssignmentsAsync(userId);
            if (blockers.Count > 0)
                return (false, "Tutor was not deactivated. Resolve scheduled lessons and outstanding replacements first. Open the status confirmation page to review them.");
        }

        string summary = SerializeStatusChange(
            user.IsActive, profile.IsActive, activate, reason);
        if (summary.Length > 1000)
            return (false, "Please shorten the reason; it contains too many special characters to record.");

        var audit = new AuditLogEntry
        {
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = actor.Id,
            ActorName = actor.UserName ?? actor.FullName ?? "Administrator",
            EventName = activate ? "TutorReactivated" : "TutorDeactivated",
            Severity = SecuritySeverity.Information,
            EntityName = nameof(TutorProfile),
            EntityKey = $"TutorProfileId={profile.TutorProfileId}",
            Summary = summary
        };
        auditIntegrity.Seal(audit);

        profile.IsActive = activate;
        profile.UpdatedAt = DateTime.UtcNow;
        user.IsActive = activate;

        // Cancel pending requests; retain the current account email.
        user.PendingTutorEmail = null;
        user.TutorEmailChangeRequestId = null;
        user.TutorEmailChangeRequestedAtUtc = null;
        user.TutorEmailChangeExpiresAtUtc = null;
        user.TutorEmailChangeRequestedByUserId = null;

        // Old cookies and setup/email tokens must not revive after reactivation.
        var identityResult = await users.UpdateSecurityStampAsync(user);
        if (!identityResult.Succeeded)
            return (false, "The account status could not be saved. Reload the page and try again.");

        // This event and both status flags are committed together.
        db.AuditLogEntries.Add(audit);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (!activate)
            return (true, "Tutor deactivated. Existing logins will be rejected on their next request. Tutor records and class history were preserved.");

        return (true, user.MustChangePassword
            ? "Tutor reactivated. Account setup is still required; resend a fresh setup email from the Tutor list."
            : "Tutor reactivated. The tutor can sign in again using the existing account and password.");
    }

    private sealed record StatusAuditPayload(
        int Version,
        bool PreviousAccountActive,
        bool PreviousProfileActive,
        bool IsActive,
        string Reason);

    public static string SerializeStatusChange(
        bool previousAccountActive, bool previousProfileActive,
        bool active, string reason)
    {
        // Stored only as JSON data. Razor encodes the reason when displaying it.
        return JsonSerializer.Serialize(new StatusAuditPayload(
            1, previousAccountActive, previousProfileActive, active, reason.Trim()),
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    public static TutorStatusHistoryItemViewModel? ReadVerifiedHistory(
        AuditLogEntry entry, AuditLogIntegrity integrity)
    {
        if (entry.EntityName != nameof(TutorProfile)
            || (entry.EventName != "TutorDeactivated" && entry.EventName != "TutorReactivated")
            || !integrity.Verify(entry)) return null;
        try
        {
            var data = JsonSerializer.Deserialize<StatusAuditPayload>(entry.Summary);
            if (data == null || data.Version != 1
                || string.IsNullOrWhiteSpace(data.Reason) || data.Reason.Length > 500
                || (entry.EventName == "TutorReactivated") != data.IsActive)
                return null;
            return new TutorStatusHistoryItemViewModel
            {
                OccurredAtUtc = entry.OccurredAtUtc,
                ActorName = entry.ActorName,
                Action = data.IsActive ? "Reactivate" : "Deactivate",
                PreviousStatus = data.PreviousAccountActive && data.PreviousProfileActive
                    ? "Active" : "Inactive",
                NewStatus = data.IsActive ? "Active" : "Inactive",
                Reason = data.Reason
            };
        }
        catch (JsonException) { return null; }
    }

    public async Task<(List<TutorStatusHistoryItemViewModel> Items, bool HasUnverified)>
        GetHistoryAsync(string userId)
    {
        int? profileId = await db.TutorProfiles.AsNoTracking()
            .Where(p => p.ApplicationUserId == userId)
            .Select(p => (int?)p.TutorProfileId).FirstOrDefaultAsync();
        var items = new List<TutorStatusHistoryItemViewModel>();
        if (!profileId.HasValue) return (items, false);
        string key = $"TutorProfileId={profileId.Value}";
        var entries = await db.AuditLogEntries.AsNoTracking()
            .Where(e => e.EntityName == nameof(TutorProfile) && e.EntityKey == key
                && (e.EventName == "TutorDeactivated" || e.EventName == "TutorReactivated"))
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.AuditLogEntryId)
            .Take(100).ToListAsync();
        bool unverified = false;
        foreach (var entry in entries)
        {
            var item = ReadVerifiedHistory(entry, auditIntegrity);
            if (item == null) unverified = true;
            else items.Add(item);
        }
        return (items, unverified);
    }
}