using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SecureSmartTuitionSystem.Services.Security;

public sealed class LoginRateLimitPolicy : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
            return RateLimitPartition.GetNoLimiter("login-page-display");

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RejectAsync;

    private static async ValueTask RejectAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        var wait = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = wait.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["Cache-Control"] = "no-store";

        const string message = "Too many sign-in attempts. Please wait before trying again.";
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.GetTypedHeaders().Accept?.Any(x => x.MediaType.Value == "application/json") == true)
        {
            await context.Response.WriteAsJsonAsync(new { message, retryAfterSeconds = wait }, cancellationToken);
            return;
        }

        // Fixed text only: never echo credentials, tokens or submitted identifiers.
        var loginUrl = WebUtility.HtmlEncode(context.Request.PathBase + "/Identity/Account/Login");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync($$"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Please wait - EduSmart</title>
<style>
body{margin:0;min-height:100vh;display:grid;place-items:center;background:#effbfc;color:#073563;font-family:system-ui,sans-serif}
main{box-sizing:border-box;width:min(92%,480px);padding:36px;background:white;border:1px solid #d8e8ec;border-radius:18px;box-shadow:0 12px 40px #07356312}
.brand{color:#008c9c;font-weight:750}h1{font-size:1.6rem;margin:20px 0 12px}p{line-height:1.6;color:#526a80}
a{display:inline-block;margin-top:12px;padding:12px 18px;background:#008c9c;color:white;text-decoration:none;border-radius:9px;font-weight:600}
a:focus-visible{outline:3px solid #073563;outline-offset:4px}
</style></head><body><main><div class="brand">EduSmart</div>
<h1>Too many sign-in attempts</h1><p>Please wait approximately {{wait}} seconds before submitting your login again.</p>
<p>You can return to the login page. Your password has not been changed.</p>
<a href="{{loginUrl}}">Back to login</a></main></body></html>
""", cancellationToken);
    }
}
