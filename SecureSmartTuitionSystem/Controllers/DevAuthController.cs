using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using System.Security.Claims;

namespace SecureSmartTuitionSystem.Controllers;

/// <summary>
/// [STUB - local testing/demo helper only, NOT one of this report's 5 modules]
/// Issues a cookie-auth session standing in for the User Authentication &amp;
/// RBAC Module's real login screen (that module belongs to the teammate and
/// is out of scope here — see README.md). Every other Controller in this
/// project only ever *consumes* [Authorize]/ClaimTypes.Role/
/// ClaimTypes.NameIdentifier, so nothing else needs to change once the real
/// login is wired in — just delete this file.
///
/// Usage: with the app running, open one of these in the browser:
///   /DevAuth/LoginAsAdmin
///   /DevAuth/LoginAsGuardian
///   /DevAuth/LoginAsStudent
///   /DevAuth/LoginAsTutor
///   /DevAuth/Logout
/// The browser will hold the login cookie afterwards, so you can then browse
/// straight to any GET endpoint (e.g. /Class/Browse, /Reporting/Dashboard),
/// and Postman/curl can reuse the same cookie for POST endpoints.
/// </summary>
public class DevAuthController : Controller
{
    private readonly AppDbContext _db;

    public DevAuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public Task<IActionResult> LoginAsAdmin() => SignInAsRoleAsync(UserRole.Admin);

    [HttpGet]
    public Task<IActionResult> LoginAsGuardian() => SignInAsRoleAsync(UserRole.Guardian);

    [HttpGet]
    public Task<IActionResult> LoginAsStudent() => SignInAsRoleAsync(UserRole.Student);

    [HttpGet]
    public Task<IActionResult> LoginAsTutor() => SignInAsRoleAsync(UserRole.Tutor);

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Content("Logged out.");
    }

    /// <summary>Shows the currently signed-in user's id/role, or "not signed in".</summary>
    [HttpGet]
    public IActionResult WhoAmI()
    {
        if (User.Identity?.IsAuthenticated != true) return Content("Not signed in.");
        return Content($"UserId: {User.FindFirstValue(ClaimTypes.NameIdentifier)}, Role: {User.FindFirstValue(ClaimTypes.Role)}");
    }

    private async Task<IActionResult> SignInAsRoleAsync(UserRole role)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Role == role);

        if (user is null)
        {
            // No such account exists yet (e.g. no Guardian/Student until a payment activates
            // one - see PaymentController.ActivateAccountsAsync) - create a throwaway one so
            // there is always something to sign in as.
            user = new ApplicationUser
            {
                UserName = $"devtest-{role}@example.com",
                Email = $"devtest-{role}@example.com",
                Role = role,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimTypes.Name, user.UserName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Content($"Signed in as {role}. UserId: {user.Id}. You can now browse to any endpoint that role can access.");
    }
}
