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
            // A guest receives a cookie challenge (302 to Login), not a direct 401.
            // Only record challenges for protected endpoints; opening Login itself is normal.
            var endpoint = context.GetEndpoint();
            var location = context.Response.Headers.Location.ToString();
            var redirectPath = location.StartsWith('/') ? location.Split('?')[0]
                : Uri.TryCreate(location, UriKind.Absolute, out var redirectUri)
                    ? redirectUri.AbsolutePath : location.Split('?')[0];
            bool guestChallenge = context.User.Identity?.IsAuthenticated != true &&
                context.Response.StatusCode == StatusCodes.Status302Found &&
                redirectPath.EndsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase) &&
                endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() == null &&
                endpoint?.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Count > 0;
            var descriptor = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
            var destination = descriptor == null ? "application endpoint" : $"{descriptor.ControllerName}.{descriptor.ActionName}";
            // Public payment links use bearer-token ownership and deliberately return 404.
            bool paymentLinkUnavailable = context.Response.StatusCode == StatusCodes.Status404NotFound &&
                descriptor?.ControllerName == "Payments";
            bool protectedNotFound = context.Response.StatusCode == StatusCodes.Status404NotFound &&
                endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() == null &&
                endpoint?.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Count > 0;
            if (context.Response.StatusCode >= 500)
            {
                await events.RecordAsync(context, "ServerError", SecuritySeverity.High,
                    $"Application returned HTTP {context.Response.StatusCode} at {destination}. Refer to private server logs using the trace ID.");
            }
            else if (protectedNotFound || paymentLinkUnavailable)
            {
                // 404 deliberately conceals ownership failures; do not reveal whether an object exists.
                await events.RecordAsync(context, "ProtectedResourceUnavailable", SecuritySeverity.Information,
                    $"Protected/payment resource unavailable (HTTP 404) at {destination}. The resource may be missing or outside the caller's permitted scope; this alone does not prove an attack.");
            }
            else if (context.Response.StatusCode is 400 or 401 or 403 or 413 or 429 || accessDeniedRedirect || guestChallenge)
            {
                int code = guestChallenge ? StatusCodes.Status401Unauthorized : accessDeniedRedirect ? StatusCodes.Status403Forbidden : context.Response.StatusCode;
                _logger.LogWarning("Request rejected: {StatusCode}; trace {TraceId}.", code, context.TraceIdentifier);
                await events.RecordAsync(context, "RequestRejected", guestChallenge ? SecuritySeverity.Information : SecuritySeverity.Warning,
                    guestChallenge
                        ? $"Guest authentication required (HTTP 401) at {destination}; redirected to Login. No request content or query string was retained."
                        : $"Request rejected with HTTP {code} at {destination}. No request content or query string was retained.");
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Browser disconnects are not critical security incidents.
            throw;
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
