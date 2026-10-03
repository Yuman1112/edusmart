using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account;
[EnableRateLimiting("LoginPolicy")]
public sealed class LoginWith2faModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly SecurityEventService _events;
    public LoginWith2faModel(SignInManager<ApplicationUser> signIn, SecurityEventService events)
    { _signIn = signIn; _events = events; }
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public sealed class InputModel
    {
        [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the six-digit authenticator code.")]
        public string Code { get; set; } = string.Empty;
    }
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _signIn.GetTwoFactorAuthenticationUserAsync();
        if (user == null || !user.IsActive || user.MustChangePassword || !user.TwoFactorEnabled)
            return RedirectToPage("/Account/Login");
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _signIn.GetTwoFactorAuthenticationUserAsync();
        if (user == null || !user.IsActive || user.MustChangePassword || !user.TwoFactorEnabled)
            return RedirectToPage("/Account/Login");
        if (!ModelState.IsValid) return Page();
        var result = await _signIn.TwoFactorAuthenticatorSignInAsync(Input.Code,
            isPersistent: false, rememberClient: false);
        if (result.Succeeded)
        {
            HttpContext.Session.Clear();
            await _events.RecordAsync(HttpContext, "TwoFactorSucceeded", SecuritySeverity.Information,
                "Authenticator sign-in completed.", user.Id);
            return LocalRedirect(!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
                ? ReturnUrl : Url.Content("~/Identity/Account/Login"));
        }
        await _events.RecordAsync(HttpContext, result.IsLockedOut ? "AccountLocked" : "LoginFailed",
            result.IsLockedOut ? SecuritySeverity.High : SecuritySeverity.Warning,
            "Second-factor sign-in was rejected.", user.Id);
        ModelState.AddModelError(string.Empty, "Sign-in could not be completed. Check your code or try again later.");
        return Page();
    }
}
