using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _db;

    public ActivityLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string? userId, string action, string? entityName = null, string? entityId = null,
        string? ipAddress = null, string? details = null, bool isSuccess = true)
    {
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            IpAddress = ipAddress,
            Details = details,
            IsSuccess = isSuccess
        });

        // Deliberately isolated from the caller's own SaveChanges so a
        // failed business transaction can still be audited (Ch.2.5:
        // auditability must be preserved even when the underlying action fails).
        await _db.SaveChangesAsync();
    }
}
