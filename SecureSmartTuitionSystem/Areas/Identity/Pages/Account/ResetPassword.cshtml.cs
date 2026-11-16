using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Services.Authentication;
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
public sealed class ResetPasswordModel(UserManager<ApplicationUser> users, IEmailOtpRecipientResolver recipients, AccountEmailLinks links,
    SecureSmartTuitionSystem.Data.ApplicationDbContext db) : PageModel
{
    [BindProperty, StringLength(450)] public string? Account { get; set; }
    [BindProperty, StringLength(8192)] public string? Code { get; set; }
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
        try { return await ResetAsync(); }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException)
        {
            db.ChangeTracker.Clear();
            LinkValid = true;
            ModelState.AddModelError(string.Empty, "The account changed or recovery is temporarily unavailable. Reload the reset link and try again.");
            return Page();
        }
    }

    private async Task<IActionResult> ResetAsync()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var user = await EligibleUserAsync();
        LinkValid = user != null;
        if (user == null || !ModelState.IsValid) return Page();

        var result = await users.ResetPasswordAsync(user, _identityCode!, Password);
        if (result.Succeeded)
        {
            await transaction.CommitAsync();
            TempData["PasswordResetSuccess"] = "Password reset. Sign in with your new password.";
            return RedirectToPage("./Login");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return Page();
    }

    private string? _identityCode;
    private async Task<ApplicationUser?> EligibleUserAsync()
    {
        if (string.IsNullOrWhiteSpace(Account) || Account.Length > 450
            || string.IsNullOrWhiteSpace(Code) || Code.Length > 8192) return null;
        var user = await users.FindByIdAsync(Account);
        if (user == null || !user.IsActive || user.MustChangePassword || !await users.HasPasswordAsync(user)) return null;
        var recipient = await recipients.ResolveAsync(user, true, HttpContext.RequestAborted);
        if (recipient == null) return null;
        if (Code.StartsWith("recovery.v1.", StringComparison.Ordinal))
        {
            var proof = links.ReadRecovery(Code);
            if (proof == null || proof.UserId != user.Id || proof.RecipientHash != EmailChangeRules.RecipientHash(recipient)) return null;
            _identityCode = proof.IdentityToken;
        }
        else
        {
            // Existing links for personal-email accounts remain usable. Guardian
            // recovery always requires the new contact-bound proof.
            if (string.IsNullOrWhiteSpace(user.Email)) return null;
            _identityCode = Code;
        }
        return await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", _identityCode)
            ? user : null;
    }
}
 
