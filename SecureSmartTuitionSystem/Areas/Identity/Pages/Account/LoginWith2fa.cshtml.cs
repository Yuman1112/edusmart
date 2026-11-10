
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

    public IActionResult OnGet()
    {
        return challenge.IsPending(HttpContext)
            ? Page()
            : RedirectToPage("./Login");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!challenge.IsPending(HttpContext))
            return RedirectToPage("./Login");

        if (!ModelState.IsValid)
            return Page();

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

        if (user == null
            || !user.IsActive
            || user.MustChangePassword
            || !await signIn.CanSignInAsync(user)
            || await signIn.UserManager.IsLockedOutAsync(user))
        {
            HttpContext.Session.Clear();

            await events.RecordAsync(
                HttpContext,
                "LoginFailed",
                SecuritySeverity.Warning,
                "Account was not eligible to sign in after email verification.",
                result.User.Id);

            ModelState.AddModelError(
                "",
                "This account is not available for sign-in. " +
                "Please contact your administrator.");

            return Page();
        }

        // Clear the pending OTP session.
        HttpContext.Session.Clear();

        // Create the authentication cookie only after verification.
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

        if (Url.IsLocalUrl(result.Destination))
            return LocalRedirect(result.Destination);

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
