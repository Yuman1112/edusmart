
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
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
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
            ILogger<LoginModel> logger,
            SecurityEventService events,
            EmailLoginChallenge challenge)
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
            string? returnUrl = null,
            string? reason = null)
        {
            ReturnUrl = returnUrl;

            if (reason == "expired")
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your session expired. Please sign in again.");
            }

            if (TempData["TwoFactorSuccess"] is string message)
            {
                ModelState.AddModelError(string.Empty, message);
            }

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

            string loginIdentifier = Input.UserId.Trim();

            ApplicationUser? user;

            // =====================================================
            // FIND USER BY EMAIL OR USER ID
            // =====================================================
            if (loginIdentifier.Contains('@'))
            {
                string? email =
                    _userManager.NormalizeEmail(loginIdentifier);

                var matches = await _userManager.Users
                    .Where(u => u.NormalizedEmail == email)
                    .Take(2)
                    .ToListAsync();

                if (matches.Count > 1)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "This email is shared by multiple accounts. " +
                        "Sign in with your User ID.");

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
                await _events.RecordAsync(
                    HttpContext,
                    "LoginFailed",
                    SecuritySeverity.Warning,
                    "Invalid sign-in attempt.");

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
                await _events.RecordAsync(
                    HttpContext,
                    "LoginFailed",
                    SecuritySeverity.Warning,
                    "Inactive account sign-in rejected.",
                    user.Id);

                ModelState.AddModelError(
                    string.Empty,
                    "This account is currently inactive.");

                return Page();
            }

            // =====================================================
            // VALIDATE PASSWORD
            // =====================================================
            // Validates password and applies account lockout.
            // Does not create an authentication cookie.
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
                await _events.RecordAsync(
                    HttpContext,
                    "AccountLocked",
                    SecuritySeverity.High,
                    "Account lockout enforced.",
                    user.Id);

                DateTimeOffset? lockoutEnd =
                    await _userManager.GetLockoutEndDateAsync(user);

                int remainingMinutes = 15;

                if (lockoutEnd.HasValue)
                {
                    TimeSpan remainingTime =
                        lockoutEnd.Value - DateTimeOffset.UtcNow;

                    remainingMinutes = Math.Max(
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
                await _events.RecordAsync(
                    HttpContext,
                    "LoginFailed",
                    SecuritySeverity.Warning,
                    "Invalid sign-in attempt.",
                    user.Id);

                int failedAttempts =
                    await _userManager.GetAccessFailedCountAsync(user);

                int remainingAttempts = Math.Max(
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
            // ADDITIONAL ACCOUNT CHECKS
            // =====================================================
            if (user.MustChangePassword)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Please complete the account setup link " +
                    "sent to your email before signing in.");

                return Page();
            }

            if (!user.IsActive ||
                await _userManager.IsLockedOutAsync(user) ||
                !await _signInManager.CanSignInAsync(user))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account is not allowed to sign in.");

                return Page();
            }

            string destination =
                Url.IsLocalUrl(ReturnUrl)
                    ? ReturnUrl!
                    : string.Empty;

            // =====================================================
            // EMAIL 2FA - CHECK USER PREFERENCE
            // =====================================================
            bool twoFactorEnabled =
                await _userManager.GetTwoFactorEnabledAsync(user);

            if (twoFactorEnabled)
            {
                // 2FA enabled:
                // Send email OTP before creating login cookie.

                string? error = await _challenge.StartAsync(
                    HttpContext,
                    user,
                    destination);

                if (error != null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error);

                    return Page();
                }

                await _events.RecordAsync(
                    HttpContext,
                    "TwoFactorChallenge",
                    SecuritySeverity.Information,
                    "Password verified; email verification required.",
                    user.Id);

                return RedirectToPage("./LoginWith2fa");
            }

            // =====================================================
            // EMAIL 2FA DISABLED - PASSWORD LOGIN
            // =====================================================
            // Password has already been verified.
            // Recheck the latest account state before signing in.

            var latestUser =
                await _userManager.FindByIdAsync(user.Id);

            if (latestUser == null ||
                !latestUser.IsActive ||
                latestUser.MustChangePassword ||
                await _userManager.IsLockedOutAsync(latestUser) ||
                !await _signInManager.CanSignInAsync(latestUser))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This account is not allowed to sign in.");

                return Page();
            }

            // Do not bypass 2FA if the setting changed
            // while this login request was being processed.
            if (await _userManager.GetTwoFactorEnabledAsync(latestUser))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your authentication settings changed. " +
                    "Please sign in again.");

                return Page();
            }

            // No OTP required when Email 2FA is disabled.
            await _signInManager.SignInAsync(
                latestUser,
                isPersistent: false);

            await _events.RecordAsync(
                HttpContext,
                "LoginSucceeded",
                SecuritySeverity.Information,
                "User signed in without Email 2FA.",
                latestUser.Id);

            if (!string.IsNullOrEmpty(destination))
            {
                return LocalRedirect(destination);
            }

            // Pass the authenticated user explicitly.
            // HttpContext.User may still contain the old
            // unauthenticated principal during this request.
            return await RedirectToDashboardAsync(latestUser);
        }

        // =========================================================
        // ROLE-BASED REDIRECTION
        // =========================================================
        private async Task<IActionResult> RedirectToDashboardAsync(
            ApplicationUser? authenticatedUser = null)
        {
            ApplicationUser? user =
                authenticatedUser ??
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

            if (await _userManager.IsInRoleAsync(user, "Parent"))
            {
                return RedirectToAction(
                    "Index",
                    "Parent",
                    new { area = "" });
            }

            if (await _userManager.IsInRoleAsync(user, "Student"))
            {
                return RedirectToAction(
                    "Index",
                    "Student",
                    new { area = "" });
            }

            return RedirectToAction(
                "Index",
                "Home",
                new { area = "" });
        }
    }
}
