using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
using System.Globalization;
using System.Security.Claims;

namespace SecureSmartTuitionSystem.Middleware;

public sealed class AuthenticatedSessionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(30);
    public AuthenticatedSessionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> users,
        SecurityEventService events)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await users.GetUserAsync(context.User);
            var now = DateTimeOffset.UtcNow;
            string? activity = context.User.FindFirstValue("EduSmart.LastActivityUtc");
            bool timedOut = !long.TryParse(activity, out long last) ||
                now.ToUnixTimeSeconds() - last >= (long)IdleLimit.TotalSeconds;
            if (user == null || !user.IsActive || user.MustChangePassword || timedOut)
            {
                await events.RecordAsync(context, "SessionTerminated", SecuritySeverity.Information,
                    timedOut ? "Authentication expired after inactivity." : "Account access is no longer allowed.");
                context.Session.Clear();
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                context.Response.Cookies.Delete(".EduSmart.Session");
                if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else context.Response.Redirect(context.Request.PathBase + "/Identity/Account/Login?reason=expired");
                return;
            }
            // Background polling does not keep an otherwise idle login alive.
            bool background = context.Request.Path.StartsWithSegments("/Messages/UnreadCounts") ||
                context.Request.Path.StartsWithSegments("/Messages/ReadStatuses") ||
                context.Request.Path.StartsWithSegments("/Messages/MarkRead") ||
                context.Request.Path.StartsWithSegments("/Security/AlertCount") ||
                (context.Request.Path.StartsWithSegments("/Assessments/Details") && context.Request.Query["fragment"] == "true");
            if (!background && now.ToUnixTimeSeconds() - last >= 60)
            {
                var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
                if (result.Principal?.Identity is ClaimsIdentity identity && result.Properties != null)
                {
                    foreach (var claim in identity.FindAll("EduSmart.LastActivityUtc").ToList()) identity.RemoveClaim(claim);
                    identity.AddClaim(new Claim("EduSmart.LastActivityUtc", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
                    await context.SignInAsync(IdentityConstants.ApplicationScheme, result.Principal, result.Properties);
                    context.User = result.Principal;
                }
            }
        }
        await _next(context);
    }
}
