using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("LoginPolicy")]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly SecurityEventService _events;
        private readonly EmailLoginChallenge _challenge;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger, SecurityEventService events, EmailLoginChallenge challenge)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _events = events;
            _challenge = challenge;
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
            string? returnUrl = null, string? reason = null)
        {
            ReturnUrl = returnUrl;
            if (reason == "expired") ModelState.AddModelError(string.Empty, "Your session expired. Please sign in again.");

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
                string? email = _userManager.NormalizeEmail(loginIdentifier);
                var matches = await _userManager.Users.Where(u => u.NormalizedEmail == email)
                    .Take(2).ToListAsync();
                if (matches.Count > 1)
                {
                    ModelState.AddModelError(string.Empty, "This email is shared by multiple accounts. Sign in with your User ID.");
                    return Page();
                }
                user = matches.SingleOrDefault();
            }
            else
            {
                user = await _userManager.FindByNameAsync(
                    loginIdentifier);
            }

            if (user == null)
            {
                await _events.RecordAsync(HttpContext, "LoginFailed", SecuritySeverity.Warning, "Invalid sign-in attempt.");
                _logger.LogWarning(
                    "Login failed for an unknown account from {RemoteIp}.",
                    HttpContext.Connection.RemoteIpAddress);

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
                await _events.RecordAsync(HttpContext, "LoginFailed", SecuritySeverity.Warning, "Inactive account sign-in rejected.", user.Id);
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

            int maximumAttempts =
                _userManager.Options
                    .Lockout
                    .MaxFailedAccessAttempts;

            // =====================================================
            // ACCOUNT LOCKOUT
            // =====================================================
            if (result.IsLockedOut)
            {
                await _events.RecordAsync(HttpContext, "AccountLocked", SecuritySeverity.High, "Account lockout enforced.", user.Id);
                DateTimeOffset? lockoutEnd =
                    await _userManager.GetLockoutEndDateAsync(user);

                int remainingMinutes = 15;

                if (lockoutEnd.HasValue)
                {
                    TimeSpan remainingTime =
                        lockoutEnd.Value - DateTimeOffset.UtcNow;

                    remainingMinutes =
                        Math.Max(
                            1,
                            (int)Math.Ceiling(
                                remainingTime.TotalMinutes));
                }

                _logger.LogWarning(
                    "Account {UserId} was temporarily locked.",
                    user.Id);

                ModelState.AddModelError(
                    string.Empty,
                    $"Your account has been temporarily locked " +
                    $"after {maximumAttempts} unsuccessful login attempts. " +
                    $"Please try again in approximately " +
                    $"{remainingMinutes} minute(s).");

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
                await _events.RecordAsync(HttpContext, "LoginFailed", SecuritySeverity.Warning, "Invalid sign-in attempt.", user.Id);
                int failedAttempts =
                    await _userManager.GetAccessFailedCountAsync(user);

                int remainingAttempts =
                    Math.Max(
                        0,
                        maximumAttempts - failedAttempts);

                _logger.LogWarning(
                    "Login failed for account {UserId}. " +
                    "{RemainingAttempts} attempt(s) remaining.",
                    user.Id,
                    remainingAttempts);

                string attemptWord =
                    remainingAttempts == 1
                        ? "attempt"
                        : "attempts";

                ModelState.AddModelError(
                    string.Empty,
                    $"Invalid User ID, email, or password. " +
                    $"You have {remainingAttempts} login " +
                    $"{attemptWord} remaining.");

                return Page();
            }

            // =====================================================
            // ADDITIONAL LOGIN STEPS
            // =====================================================
            if (user.MustChangePassword)
            {
                ModelState.AddModelError(string.Empty, "Please complete the account setup link sent to your email before signing in.");
                return Page();
            }
            var destination = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : string.Empty;
            var error = await _challenge.StartAsync(HttpContext, user, destination);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                return Page();
            }

            await _events.RecordAsync(HttpContext, "TwoFactorChallenge", SecuritySeverity.Information,
                "Password verified; email verification required.", user.Id);
            return RedirectToPage("./LoginWith2fa");
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

