namespace SecureSmartTuitionSystem.Middleware
{
    public sealed class SecurityMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMonitoringMiddleware> _logger;

        public SecurityMonitoringMiddleware(
            RequestDelegate next,
            ILogger<SecurityMonitoringMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context)
        {
            try
            {
                await _next(context);

                if (context.Response.StatusCode is
                    StatusCodes.Status401Unauthorized or
                    StatusCodes.Status403Forbidden or
                    StatusCodes.Status429TooManyRequests)
                {
                    _logger.LogWarning(
                        "Security response {StatusCode} for {Method} {Path}. Trace {TraceId}.",
                        context.Response.StatusCode,
                        context.Request.Method,
                        context.Request.Path.Value,
                        context.TraceIdentifier);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unhandled request failure for {Method} {Path}. Trace {TraceId}.",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.TraceIdentifier);

                throw;
            }
        }
    }
}
