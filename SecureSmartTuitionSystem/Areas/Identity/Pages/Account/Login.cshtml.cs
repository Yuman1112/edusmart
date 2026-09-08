using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public LoginModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Please enter your User ID.")]
            [Display(Name = "User ID")]
            public string UserId { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please enter your password.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByNameAsync(
                Input.UserId.Trim());

            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid login attempt.");

                return Page();
            }

            // ??????????????????????
            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                Input.Password,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Account temporarily locked. Please try again later.");

                return Page();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid login attempt.");

                return Page();
            }

            StatusMessage = user.MustChangePassword
                ? "Password verified. First-login password change is the next step."
                : "Password verified. Email verification is the next step.";

            return Page();
        }
    }
}