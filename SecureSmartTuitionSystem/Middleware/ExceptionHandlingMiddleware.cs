using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using System.Net;

namespace SecureSmartTuitionSystem.Middleware;

/// <summary>
/// System Security Module: "Error Handling" (Table 4.33 / Ch.2.5).
/// Unhandled exceptions are caught here so that a generic message and a
/// correlation TraceId are the only things ever returned to the client —
/// stack traces and connection strings never leave the server, consistent
/// with OWASP's guidance against verbose error disclosure.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

            try
            {
                // Best-effort: persist full details server-side for auditing/debugging.
                // Uses a short-lived scope so a database failure here can't mask the original error.
                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ErrorLogs.Add(new ErrorLog
                {
                    TraceId = traceId,
                    Path = context.Request.Path,
                    ExceptionType = ex.GetType().FullName,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace
                });
                await db.SaveChangesAsync();
            }
            catch
            {
                // Logging must never itself crash the error handler.
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            await context.Response.WriteAsJsonAsync(new
            {
                message = "An unexpected error occurred. Please try again, and quote this reference if it persists.",
                traceId
            });
        }
    }
}
