using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("PublicFormPolicy")]
    public class SetupPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser>
            _userManager;

        private readonly SignInManager<ApplicationUser>
            _signInManager;

        public SetupPasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string UserId { get; set; }
                = string.Empty;

            [Required]
            public string Token { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Please enter a new password.")]
            [DataType(DataType.Password)]
            [StringLength(
                100,
                MinimumLength = 8,
                ErrorMessage =
                    "The password must be at least 8 characters.")]
            public string Password { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Please confirm the new password.")]
            [DataType(DataType.Password)]
            [Compare(
                nameof(Password),
                ErrorMessage =
                    "The passwords do not match.")]
            public string ConfirmPassword { get; set; }
                = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(
            string? userId,
            string? token)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(
                    "DEBUG: userId is missing.");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(
                    "DEBUG: token is missing.");
            }

            var user =
                await _userManager.FindByIdAsync(
                    userId);

            if (user == null)
            {
                return BadRequest(
                    $"DEBUG: User was not found. Received userId = [{userId}]");
            }

            if (!user.IsActive)
            {
                return BadRequest(
                    "DEBUG: User exists but account is inactive.");
            }

            Input.UserId =
                userId;

            Input.Token =
                token;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user =
                await _userManager.FindByIdAsync(
                    Input.UserId);

            if (user == null ||
                !user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The account setup link is invalid.");

                return Page();
            }

            string decodedToken;

            try
            {
                byte[] tokenBytes =
                    WebEncoders.Base64UrlDecode(
                        Input.Token);

                decodedToken =
                    Encoding.UTF8.GetString(
                        tokenBytes);
            }
            catch
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The account setup link is invalid.");

                return Page();
            }

            IdentityResult result =
                await _userManager.ResetPasswordAsync(
                    user,
                    decodedToken,
                    Input.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return Page();
            }

            user.MustChangePassword =
                false;

            IdentityResult updateResult =
                await _userManager.UpdateAsync(
                    user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return Page();
            }

            await _signInManager.SignOutAsync();

            TempData["SuccessMessage"] =
                "Your password has been created successfully. " +
                $"Your User ID is {user.UserName}. You can now sign in.";

            return RedirectToPage(
                "/Account/Login",
                new { area = "Identity" });
        }
    }
}
