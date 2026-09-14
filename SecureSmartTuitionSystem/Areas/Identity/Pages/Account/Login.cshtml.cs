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

        // =========================================================
        // GET - LOGIN
        // =========================================================
        public async Task<IActionResult> OnGetAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            // If already logged in, redirect to the appropriate page.
            if (User.Identity?.IsAuthenticated == true)
            {
                return await RedirectToDashboardAsync();
            }

            return Page();
        }

        // =========================================================
        // POST - LOGIN
        // =========================================================
        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? ReturnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Remove accidental spaces before/after User ID or Email.
            string loginIdentifier = Input.UserId.Trim();

            ApplicationUser? user;

            // =====================================================
            // FIND USER BY EMAIL OR USER ID
            // =====================================================
            if (loginIdentifier.Contains('@'))
            {
                user = await _userManager.FindByEmailAsync(
                    loginIdentifier);
            }
            else
            {
                user = await _userManager.FindByNameAsync(
                    loginIdentifier);
            }

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid User ID, email, or password.");

                return Page();
            }

            // =====================================================
            // CHECK ACCOUNT STATUS
            // =====================================================
            if (!user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account is currently inactive.");

                return Page();
            }

            // =====================================================
            // VALIDATE PASSWORD
            // =====================================================
            // CheckPasswordSignInAsync validates the password and
            // counts failed attempts toward account lockout.
            // It does not create the login cookie yet.
            var result =
                await _signInManager.CheckPasswordSignInAsync(
                    user,
                    Input.Password,
                    lockoutOnFailure: true);

            // =====================================================
            // ACCOUNT LOCKOUT
            // =====================================================
            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account has been temporarily locked. " +
                    "Please try again later.");

                return Page();
            }

            // =====================================================
            // LOGIN NOT ALLOWED
            // =====================================================
            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account is not allowed to log in.");

                return Page();
            }

            // =====================================================
            // INVALID PASSWORD
            // =====================================================
            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid User ID, email, or password.");

                return Page();
            }

            // =====================================================
            // ADDITIONAL LOGIN STEPS
            // =====================================================
            // Forced password change and 2FA will be implemented
            // later. Accounts requiring these steps must not
            // bypass them.
            if (user.MustChangePassword ||
                user.TwoFactorEnabled)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account requires an additional login " +
                    "step that is not available yet.");

                return Page();
            }

            // =====================================================
            // SIGN IN
            // =====================================================
            // All checks have passed. Create the authentication
            // cookie and sign the user in.
            await _signInManager.SignInAsync(
                user,
                isPersistent: false);

            // =====================================================
            // RETURN URL
            // =====================================================
            // If the user was redirected to Login from a protected
            // local page, return to that page after successful login.
            if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
                Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            // Otherwise redirect according to the user's role.
            return await RedirectToDashboardAsync();
        }

        // =========================================================
        // ROLE-BASED REDIRECTION
        // =========================================================
        private async Task<IActionResult> RedirectToDashboardAsync()
        {
            ApplicationUser? user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Admin
            if (await _userManager.IsInRoleAsync(
                    user,
                    "Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "Admin",
                    new { area = "" });
            }

            // Tutor
            if (await _userManager.IsInRoleAsync(
                    user,
                    "Tutor"))
            {
                return RedirectToAction(
                    "Index",
                    "Tutor",
                    new { area = "" });
            }

            // Parent
            if (await _userManager.IsInRoleAsync(
                    user,
                    "Parent"))
            {
                return RedirectToAction(
                    "Index",
                    "Parent",
                    new { area = "" });
            }

            // Student
            if (await _userManager.IsInRoleAsync(
                    user,
                    "Student"))
            {
                return RedirectToAction(
                    "Index",
                    "Student",
                    new { area = "" });
            }

            // Fallback
            return RedirectToAction(
                "Index",
                "Home",
                new { area = "" });
        }
    }
}