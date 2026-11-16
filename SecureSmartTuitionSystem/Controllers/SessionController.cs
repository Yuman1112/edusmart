using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Controllers;
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("Session")]
public sealed class SessionController : Controller
{
    private IdleSessionState.Entry? Current()
    {
        if (User.Identity?.IsAuthenticated != true || HttpContext.Items[IdleSessionState.TicketKey] is not string id) return null;
        return IdleSessionState.Find(id);
    }
    [HttpGet("Status")]
    public IActionResult Status() => Snapshot(false);
    [HttpPost("Activity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activity()
    {
        var state = Current();
        var now = DateTimeOffset.UtcNow;
        if (state == null || !state.Check(now, true)) return Unauthorized();
        // Refresh the authentication ticket too, without letting automatic
        // cookie sliding expiration override the server's idle deadline.
        var ticket = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (ticket.Principal == null || ticket.Properties == null) return Unauthorized();
        ticket.Properties.IssuedUtc = now;
        ticket.Properties.ExpiresUtc = now + IdleSessionState.Limit;
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, ticket.Principal, ticket.Properties);
        return Snapshot(false);
    }
    private IActionResult Snapshot(bool renew)
    {
        var state = Current();
        var now = DateTimeOffset.UtcNow;
        if (state == null || !state.Check(now, renew)) return Unauthorized();
        return Json(new { serverNow = now.ToUnixTimeMilliseconds(), expiresAt = state.Expires.ToUnixTimeMilliseconds(),
            scope = HttpContext.Items[IdleSessionState.TicketKey] });
    }
}
