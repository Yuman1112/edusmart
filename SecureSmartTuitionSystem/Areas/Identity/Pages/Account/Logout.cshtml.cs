using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account;

public sealed class LogoutModel : PageModel
{
    private readonly SecurityEventService _events;
    private readonly UserManager<ApplicationUser> _users;
    public LogoutModel(SecurityEventService events, UserManager<ApplicationUser> users)
    { _events = events; _users = users; }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        await _events.RecordAsync(HttpContext, "Logout", SecuritySeverity.Information, "User signed out.");
        var user = await _users.GetUserAsync(User);
        if (user != null)
        {
            var revoked = await _users.UpdateSecurityStampAsync(user);
            if (!revoked.Succeeded) throw new InvalidOperationException("Session revocation failed.");
        }
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
        Response.Cookies.Delete(".EduSmart.Session");
        return LocalRedirect(Url.Content("~/Identity/Account/Login"));
    }
}
