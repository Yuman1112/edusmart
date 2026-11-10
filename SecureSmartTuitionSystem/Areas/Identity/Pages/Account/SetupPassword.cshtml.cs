using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("PublicFormPolicy")]
    public class SetupPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SetupPasswordModel> _logger;

        public SetupPasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            ILogger<SetupPasswordModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [StringLength(450)]
            public string UserId { get; set; } = "";

            [Required]
            [StringLength(8192)]
            public string Token { get; set; } = "";

            [Required(
                ErrorMessage = "Please enter a new password.")]
            [DataType(DataType.Password)]
            [StringLength(
                100,
                MinimumLength = 8,
                ErrorMessage =
                    "The password must be at least 8 characters.")]
            public string Password { get; set; } = "";

            [Required(
                ErrorMessage = "Please confirm the new password.")]
            [DataType(DataType.Password)]
            [Compare(
                nameof(Password),
                ErrorMessage = "The passwords do not match.")]
            public string ConfirmPassword { get; set; } = "";
        }

        public async Task<IActionResult> OnGetAsync(
            string? userId,
            string? token)
        {
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["Cache-Control"] = "no-store";

            if (string.IsNullOrWhiteSpace(userId)
                || userId.Length > 450
                || string.IsNullOrWhiteSpace(token)
                || token.Length > 8192)
            {
                return BadRequest(
                    "The account setup link is invalid or expired.");
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(account => account.Id == userId);

            if (!CanSetUpAccount(user))
            {
                return BadRequest(
                    "The account setup link is invalid or unavailable. "
                    + "If your email is being changed, complete "
                    + "the new email verification first.");
            }

            string? decodedToken = DecodeToken(token);

            if (decodedToken == null)
            {
                return BadRequest(
                    "The account setup link is invalid or expired.");
            }

            bool tokenValid =
                await _userManager.VerifyUserTokenAsync(
                    user!,
                    _userManager.Options.Tokens.PasswordResetTokenProvider,
                    "ResetPassword",
                    decodedToken);

            if (!tokenValid)
            {
                return BadRequest(
                    "The account setup link is invalid or expired.");
            }

            Input.UserId = userId;
            Input.Token = token;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["Cache-Control"] = "no-store";

            if (!ModelState.IsValid)
                return Page();

            string? decodedToken = DecodeToken(Input.Token);

            if (decodedToken == null)
            {
                ModelState.AddModelError(
                    "",
                    "The account setup link is invalid or expired.");

                return Page();
            }

            string userName;

            try
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var user = await _db.Users
                    .FirstOrDefaultAsync(
                        account => account.Id == Input.UserId);

                if (!CanSetUpAccount(user))
                {
                    ModelState.AddModelError(
                        "",
                        "The account setup link is invalid or unavailable. "
                        + "If your email is being changed, complete "
                        + "the new email verification first.");

                    return Page();
                }

                // Account setup is completed through a verified email link.
                user!.MustChangePassword = false;

                // A guardian-delivered setup link does not confirm a student email.
                user.EmailConfirmed = !string.IsNullOrWhiteSpace(user.Email);

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
                            "",
                            error.Description);
                    }

                    return Page();
                }

                await transaction.CommitAsync();

                userName = user.UserName ?? "";
            }
            catch (Exception ex) when (
                ex is DbUpdateException
                || ex is System.Data.Common.DbException)
            {
                _logger.LogWarning(
                    "Account setup failed for {UserId}. "
                    + "Error type: {ErrorType}.",
                    Input.UserId,
                    ex.GetType().Name);

                ModelState.AddModelError(
                    "",
                    "Unable to complete account setup. "
                    + "Please open the setup link again and try.");

                return Page();
            }

            await _signInManager.SignOutAsync();

            TempData["SuccessMessage"] =
                "Your password has been created successfully. "
                + $"Your User ID is {userName}. You can now sign in.";

            return RedirectToPage(
                "/Account/Login",
                new { area = "Identity" });
        }

        private static bool CanSetUpAccount(ApplicationUser? user)
        {
            return user != null
                && user.IsActive
                && user.MustChangePassword
                && !user.TutorEmailChangeRequestId.HasValue
                && string.IsNullOrWhiteSpace(user.PendingTutorEmail);
        }

        private static string? DecodeToken(string encodedToken)
        {
            try
            {
                byte[] tokenBytes =
                    WebEncoders.Base64UrlDecode(encodedToken);

                return Encoding.UTF8.GetString(tokenBytes);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}