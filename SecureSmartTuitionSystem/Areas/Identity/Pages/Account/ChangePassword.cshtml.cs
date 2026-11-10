using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChangePasswordModel(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn) : PageModel
{
    private const string CurrentPasswordField = "Input." + nameof(InputModel.OldPassword);
    private const string NewPasswordField = "Input." + nameof(InputModel.NewPassword);

    public const string DifferentPasswordMessage = "The new password must be different from your current password.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public PasswordOptions PasswordRules => users.Options.Password;

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Please enter your current password.")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = "";

        [Required(ErrorMessage = "Please enter a new password.")]
        [StringLength(100, ErrorMessage = "The new password must be 100 characters or fewer.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge();
        if (!user.IsActive || user.MustChangePassword)
            return Forbid();

        return await users.HasPasswordAsync(user)
            ? Page()
            : RedirectToPage("/Account/Manage/SetPassword");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge();
        if (!user.IsActive || user.MustChangePassword)
            return Forbid();
        if (!await users.HasPasswordAsync(user))
            return RedirectToPage("/Account/Manage/SetPassword");
        if (!ModelState.IsValid)
            return Page();

        if (string.Equals(Input.OldPassword, Input.NewPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(NewPasswordField, DifferentPasswordMessage);
            return Page();
        }

        var result = await users.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                switch (error.Code)
                {
                    case nameof(IdentityErrorDescriber.PasswordMismatch):
                        ModelState.AddModelError(CurrentPasswordField, "Incorrect current password. Please try again.");
                        break;
                    case nameof(IdentityErrorDescriber.PasswordTooShort):
                    case nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric):
                    case nameof(IdentityErrorDescriber.PasswordRequiresDigit):
                    case nameof(IdentityErrorDescriber.PasswordRequiresLower):
                    case nameof(IdentityErrorDescriber.PasswordRequiresUpper):
                    case nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars):
                        ModelState.AddModelError(NewPasswordField, error.Description);
                        break;
                    default:
                        ModelState.AddModelError(string.Empty, "Unable to update your password. Please try again.");
                        break;
                }
            }

            return Page();
        }

        await signIn.RefreshSignInAsync(user);
        StatusMessage = "Your password has been updated successfully.";
        return RedirectToPage();
    }
}
