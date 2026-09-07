namespace SecureSmartTuitionSystem.Services;

/// <summary>
/// System Security Module: records every state-changing action together
/// with the acting user's identity and a timestamp (Ch.2.5 and Functional
/// Requirements: "maintain an activity log of security-relevant actions,
/// including enrollment approval and payment confirmation, to support
/// auditing").
/// </summary>
public interface IActivityLogService
{
    Task LogAsync(string? userId, string action, string? entityName = null, string? entityId = null,
        string? ipAddress = null, string? details = null, bool isSuccess = true);
}
