using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
using System.Security.Claims;

namespace SecureSmartTuitionSystem.Middleware;

public sealed class AuthenticatedSessionMiddleware
{
    private readonly RequestDelegate _next;
    public AuthenticatedSessionMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> users, SecurityEventService events)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var ticket = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            var user = await users.GetUserAsync(context.User);
            var now = DateTimeOffset.UtcNow;
            var props = ticket.Properties;
            string? id = null;
            if (props != null) props.Items.TryGetValue(IdleSessionState.TicketKey, out id);
            bool created = false;
            // Upgrade an existing, still-valid login once. An unknown existing ID
            // is never resurrected from its cookie after expiration or restart.
            if (id == null && props != null && long.TryParse(context.User.FindFirstValue("EduSmart.LastActivityUtc"), out var seconds)
                && seconds <= now.ToUnixTimeSeconds() && now.ToUnixTimeSeconds() - seconds < 1800)
            {
                id = IdleSessionState.Create(DateTimeOffset.FromUnixTimeSeconds(seconds));
                props.Items[IdleSessionState.TicketKey] = id;
                created = true;
            }
            var state = id == null ? null : IdleSessionState.Find(id);
            bool timedOut = state == null || !state.Check(now, false);
            if (user == null || !user.IsActive || user.MustChangePassword || timedOut)
            {
                await events.RecordAsync(context, "SessionTerminated", SecuritySeverity.Information,
                    timedOut ? "Authentication expired after inactivity." : "Account access is no longer allowed.");
                context.Session.Clear();
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                context.Response.Cookies.Delete(".EduSmart.Session");
                if (context.Request.Path.StartsWithSegments("/Session") || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else context.Response.Redirect(context.Request.PathBase + "/Identity/Account/Login?reason=" + (timedOut ? "expired" : "access"));
                return;
            }
            context.Items[IdleSessionState.TicketKey] = id;
            bool navigation = context.Request.Headers["Sec-Fetch-Mode"] == "navigate" &&
                context.Request.Headers["Sec-Fetch-User"] == "?1";
            if (navigation && !state!.Check(now, true))
            {
                context.Response.Redirect(context.Request.PathBase + "/Identity/Account/Login?reason=expired");
                return;
            }
            if (navigation && props != null)
            {
                props.IssuedUtc = now;
                props.ExpiresUtc = now + IdleSessionState.Limit;
            }
            // Only deliberate activity renews the timer. Polls, media ranges and
            // all other background requests cannot keep a login alive.
            if ((created || navigation) && ticket.Principal != null && props != null)
                await context.SignInAsync(IdentityConstants.ApplicationScheme, ticket.Principal, props);
        }
        await _next(context);
    }
}
