
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("EmailCodePolicy")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class LoginWith2faModel(
    EmailLoginChallenge challenge,
    SignInManager<ApplicationUser> signIn,
    SecurityEventService events) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Enter the verification code.")]
    [RegularExpression(
        "^[0-9]{6}$",
        ErrorMessage = "Enter the six-digit verification code.")]
    public string Code { get; set; } = "";

    // =========================================================
    // GET - EMAIL OTP VERIFICATION PAGE
    // =========================================================
    public IActionResult OnGet()
    {
        return challenge.IsPending(HttpContext)
            ? Page()
            : RedirectToPage("./Login");
    }

    // =========================================================
    // POST - VERIFY EMAIL OTP
    // =========================================================
    public async Task<IActionResult> OnPostAsync()
    {
        if (!challenge.IsPending(HttpContext))
        {
            return RedirectToPage("./Login");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await challenge.VerifyAsync(
            HttpContext,
            Code);

        if (result.User == null)
        {
            ModelState.AddModelError(
                nameof(Code),
                result.Error ?? "Invalid verification code.");

            return Page();
        }

        // Reload the latest account state before signing in.
        var user = await signIn.UserManager.FindByIdAsync(
            result.User.Id);

        // Ensure that Email 2FA is still enabled.
        // Also recheck account status and lockout.
        if (user == null ||
            !user.IsActive ||
            user.MustChangePassword ||
            !await signIn.UserManager
                .GetTwoFactorEnabledAsync(user) ||
            !await signIn.CanSignInAsync(user) ||
            await signIn.UserManager.IsLockedOutAsync(user))
        {
            challenge.ClearPending(HttpContext);

            await events.RecordAsync(
                HttpContext,
                "LoginFailed",
                SecuritySeverity.Warning,
                "Account was not eligible to sign in after email verification.",
                result.User.Id);

            ModelState.AddModelError(
                string.Empty,
                "This account is not available for sign-in. " +
                "Please contact your administrator.");

            return Page();
        }

        // Remove only the pending login challenge.
        challenge.ClearPending(HttpContext);

        // Create the authentication cookie after OTP verification.
        await signIn.SignInAsync(
            user,
            isPersistent: false,
            authenticationMethod: "email_otp");

        await events.RecordAsync(
            HttpContext,
            "LoginSucceeded",
            SecuritySeverity.Information,
            "User signed in after email verification.",
            user.Id);

        // Redirect only to a local URL.
        if (Url.IsLocalUrl(result.Destination))
        {
            return LocalRedirect(result.Destination);
        }

        // Role-based dashboard redirection.
        var roles =
            await signIn.UserManager.GetRolesAsync(user);

        var controller = new[]
        {
            "Admin",
            "Tutor",
            "Parent",
            "Student"
        }.FirstOrDefault(roles.Contains) ?? "Home";

        return RedirectToAction(
            "Index",
            controller,
            new { area = "" });
    }
}
