using Microsoft.AspNetCore.Mvc.Filters;
using SecureSmartTuitionSystem.Services;
using System.Security.Claims;

namespace SecureSmartTuitionSystem.Filters;

/// <summary>
/// Applies to every state-changing (POST/PUT/DELETE) action carrying an
/// <see cref="AuditAttribute"/>. Logs the action, acting user, and outcome
/// to ActivityLogs — System Security Module (Ch.2.5 / Functional
/// Requirements: "maintain an activity log of security-relevant actions").
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    private readonly IActivityLogService _activityLog;

    public AuditActionFilter(IActivityLogService activityLog)
    {
        _activityLog = activityLog;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auditAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<AuditAttribute>()
            .FirstOrDefault();

        var executedContext = await next();

        if (auditAttribute is null) return;

        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var isSuccess = executedContext.Exception is null;

        await _activityLog.LogAsync(
            userId: userId,
            action: auditAttribute.Action,
            entityName: auditAttribute.EntityName,
            ipAddress: ipAddress,
            isSuccess: isSuccess,
            details: isSuccess ? null : executedContext.Exception?.Message);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class AuditAttribute : Attribute
{
    public string Action { get; }
    public string? EntityName { get; }

    public AuditAttribute(string action, string? entityName = null)
    {
        Action = action;
        EntityName = entityName;
    }
}
