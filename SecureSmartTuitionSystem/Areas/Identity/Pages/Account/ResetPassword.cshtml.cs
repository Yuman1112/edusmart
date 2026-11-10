using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("PasswordResetPolicy")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResetPasswordModel(UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty, StringLength(450)] public string? Account { get; set; }
    [BindProperty, StringLength(2048)] public string? Code { get; set; }
    [BindProperty, Required, DataType(DataType.Password), StringLength(128)] public string Password { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords must match.")]
    public string ConfirmPassword { get; set; } = "";
    public bool LinkValid { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? account, string? code)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Account = account;
        Code = code;
        LinkValid = await EligibleUserAsync() != null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var user = await EligibleUserAsync();
        LinkValid = user != null;
        if (user == null || !ModelState.IsValid) return Page();

        var result = await users.ResetPasswordAsync(user, Code!, Password);
        if (result.Succeeded)
        {
            TempData["PasswordResetSuccess"] = "Password reset. Sign in with your new password.";
            return RedirectToPage("./Login");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return Page();
    }

    private async Task<ApplicationUser?> EligibleUserAsync()
    {
        if (string.IsNullOrWhiteSpace(Account) || Account.Length > 450
            || string.IsNullOrWhiteSpace(Code) || Code.Length > 2048) return null;
        var user = await users.FindByIdAsync(Account);
        if (user == null || !user.IsActive || user.MustChangePassword || !await users.HasPasswordAsync(user)) return null;
        return await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", Code)
            ? user : null;
    }
}
