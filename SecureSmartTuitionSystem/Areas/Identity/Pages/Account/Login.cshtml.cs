using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "User ID or Email is required.")]
            [Display(Name = "User ID or Email")]
            public string UserId { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (User.Identity?.IsAuthenticated == true)
            {
                return await RedirectToDashboardAsync();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? ReturnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            ApplicationUser? user;

            if (Input.UserId.Contains('@'))
            {
                user = await _userManager.FindByEmailAsync(
                    Input.UserId.Trim());
            }
            else
            {
                user = await _userManager.FindByNameAsync(
                    Input.UserId.Trim());
            }

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid User ID, email, or password.");

                return Page();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account is currently inactive.");

                return Page();
            }

            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    Input.Password,
                    isPersistent: false,
                    lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
                    Url.IsLocalUrl(ReturnUrl))
                {
                    return LocalRedirect(ReturnUrl);
                }

                return await RedirectToDashboardAsync();
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account has been temporarily locked. " +
                    "Please try again later.");

                return Page();
            }

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account is not allowed to log in.");

                return Page();
            }

            ModelState.AddModelError(
                string.Empty,
                "Invalid User ID, email, or password.");

            return Page();
        }

        private async Task<IActionResult> RedirectToDashboardAsync()
        {
            ApplicationUser? user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "Admin",
                    new { area = "" });
            }

            if (await _userManager.IsInRoleAsync(user, "Tutor"))
            {
                return RedirectToAction(
                    "Index",
                    "Tutor",
                    new { area = "" });
            }

            return RedirectToAction(
                "Index",
                "Home",
                new { area = "" });
        }
    }
}