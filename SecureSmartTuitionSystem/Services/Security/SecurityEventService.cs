using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Security;
using System.Security.Claims;

namespace SecureSmartTuitionSystem.Services.Security;

public sealed class SecurityEventService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AuditLogIntegrity _integrity;
    private readonly ILogger<SecurityEventService> _logger;
    public SecurityEventService(IServiceScopeFactory scopes, AuditLogIntegrity integrity,
        ILogger<SecurityEventService> logger)
    {
        _scopes = scopes; _integrity = integrity; _logger = logger;
    }

    public async Task RecordAsync(HttpContext? context, string eventName,
        SecuritySeverity severity, string summary, string? actorUserId = null)
    {
        // Callers supply fixed summaries. Never pass bodies, credentials, tokens or query strings.
        var entry = new AuditLogEntry {
            EventName = Limit(eventName, 100), Severity = severity,
            ActorUserId = actorUserId ?? context?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = Limit(context?.User.Identity?.Name ?? "Anonymous / System", 256),
            RemoteIp = Limit(context?.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 64),
            TraceId = Limit(context?.TraceIdentifier ?? string.Empty, 120),
            Summary = Limit(summary, 1000)
        };
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!string.IsNullOrWhiteSpace(actorUserId))
            {
                var name = await db.Users.AsNoTracking().Where(x => x.Id == actorUserId)
                    .Select(x => x.UserName).FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(name)) entry.ActorName = Limit(name, 256);
            }
            if (severity == SecuritySeverity.Warning && entry.RemoteIp.Length > 0 &&
                eventName is "LoginFailed" or "RequestRejected" or "UploadRejected")
            {
                var since = DateTime.UtcNow.AddMinutes(-15);
                int recent = await db.AuditLogEntries.AsNoTracking().CountAsync(x =>
                    x.EventName == eventName && x.RemoteIp == entry.RemoteIp && x.OccurredAtUtc >= since);
                if (recent >= 4) entry.Severity = SecuritySeverity.High;
            }
            _integrity.Seal(entry);
            db.AuditLogEntries.Add(entry);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Request denial must still work if the database is unavailable.
            _logger.LogError(ex, "Security event persistence failed. Event {Event}; trace {TraceId}.",
                eventName, entry.TraceId);
        }
    }

    public static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}
