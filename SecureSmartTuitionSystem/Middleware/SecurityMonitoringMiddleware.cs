using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Middleware;

public sealed class SecurityMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityMonitoringMiddleware> _logger;
    public SecurityMonitoringMiddleware(RequestDelegate next, ILogger<SecurityMonitoringMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context, SecurityEventService events)
    {
        try
        {
            await _next(context);
            bool accessDeniedRedirect = context.Response.StatusCode == StatusCodes.Status302Found &&
                context.Response.Headers.Location.ToString().Contains("/Identity/Account/AccessDenied", StringComparison.Ordinal);
            if (context.Response.StatusCode is 400 or 401 or 403 or 413 or 429 || accessDeniedRedirect)
            {
                int code = accessDeniedRedirect ? StatusCodes.Status403Forbidden : context.Response.StatusCode;
                _logger.LogWarning("Request rejected: {StatusCode}; trace {TraceId}.", code, context.TraceIdentifier);
                await events.RecordAsync(context, "RequestRejected", SecuritySeverity.Warning,
                    $"Request rejected with HTTP {code}. No request content was retained.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled request failure; trace {TraceId}.", context.TraceIdentifier);
            await events.RecordAsync(context, "UnhandledError", SecuritySeverity.Critical,
                "An unexpected application error occurred. Refer to the private server log using the trace ID.");
            throw;
        }
    }
}
