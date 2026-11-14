
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor,Parent,Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountSettingsController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    AccountSecurityMail mail,
    EmailOtpRecipientResolver recipients,
    IDataProtectionProvider dataProtection,
    ILogger<AccountSettingsController> logger) : Controller
{
    private const string SessionKey =
        "EduSmart.AccountSettings.TwoFactor";

    private const int MaxAttempts = 5;

    private static readonly TimeSpan CodeLifetime =
        TimeSpan.FromMinutes(5);


    private readonly IDataProtector protector =
        dataProtection.CreateProtector(
            "EduSmart.AccountSettings.TwoFactor.v1");

    private sealed record PendingChange(
        string UserId,
        bool Enable,
        string Code,
        string SecurityStamp,
        EmailOtpRecipient Recipient,
        DateTimeOffset IssuedAt,
        int RemainingAttempts);

    public sealed class TwoFactorViewModel
    {
        public bool Enabled { get; set; }
        public bool EmailVerified { get; set; }
        public string? Email { get; set; }
        public bool UsesGuardianEmail { get; set; }
        public bool Pending { get; set; }
        public bool RequestedEnable { get; set; }
        public int RemainingAttempts { get; set; }
    }

    public sealed class BeginChangeInput
    {
        [Required]
        public string Password { get; set; } = "";

        public bool Enable { get; set; }
    }

    public sealed class ConfirmChangeInput
    {
        [Required]
        [RegularExpression("^[0-9]{6}$")]
        public string Code { get; set; } = "";
    }

    private bool AllowOperation(ApplicationUser user, string operation)
    {
        int seconds = SecureSmartTuitionSystem.Services.Security.TwoFactorRequestLimits.TryAcquire(
            user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", operation);
        if (seconds == 0) return true;
        TempData["TwoFactorInfo"] = $"Too many requests. Please wait {seconds} seconds before trying again.";
        return false;
    }

    [HttpGet]
    public async Task<IActionResult> TwoFactor()
    {
        var user = await GetUsableUserAsync();

        if (user == null)
            return Forbid();

        var recipient = await recipients.ResolveAsync(
            user, requireVerifiedOwnEmail: true, HttpContext.RequestAborted);
        var pending = await GetValidPendingAsync(user, recipient);

        return View(new TwoFactorViewModel
        {
            Enabled = await users.GetTwoFactorEnabledAsync(user),
            EmailVerified = recipient != null,
            Email = user.Email,
            UsesGuardianEmail = recipient?.GuardianLinkId != null,
            Pending = pending != null,
            RequestedEnable = pending?.Enable ?? false,
            RemainingAttempts =
                pending?.RemainingAttempts ?? 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginChange(
        BeginChangeInput input)
    {
        var user = await GetUsableUserAsync();

        if (user == null)
            return Forbid();

        if (!AllowOperation(user, "password"))
            return RedirectToAction(nameof(TwoFactor));

        if (!ModelState.IsValid)
        {
            TempData["TwoFactorError"] =
                "Please enter your current password.";

            return RedirectToAction(nameof(TwoFactor));
        }

        bool current =
            await users.GetTwoFactorEnabledAsync(user);

        if (current == input.Enable)
        {
            TempData["TwoFactorInfo"] =
                "Your 2FA setting is already up to date.";

            return RedirectToAction(nameof(TwoFactor));
        }

        var recipient = await recipients.ResolveAsync(
            user, requireVerifiedOwnEmail: true, HttpContext.RequestAborted);
        if (recipient == null)
        {
            TempData["TwoFactorError"] =
                "A verified email address is required " +
                "to change Email 2FA settings.";

            return RedirectToAction(nameof(TwoFactor));
        }

        // Check the current password without signing in again.
        bool passwordValid =
            await users.CheckPasswordAsync(
                user,
                input.Password);

        if (!passwordValid)
        {
            TempData["TwoFactorError"] =
                "Incorrect current password.";

            return RedirectToAction(nameof(TwoFactor));
        }

        if (!AllowOperation(user, "send"))
            return RedirectToAction(nameof(TwoFactor));

        string code =
            RandomNumberGenerator.GetInt32(1000000)
                .ToString("D6", CultureInfo.InvariantCulture);

        var pending = new PendingChange(
            user.Id,
            input.Enable,
            code,
            await users.GetSecurityStampAsync(user),
            recipient,
            DateTimeOffset.UtcNow,
            MaxAttempts);

        SavePending(pending);

        try
        {
            await mail.SendAsync(
                recipient.Address,
                "EduSmart - Confirm Your 2FA Change",
                SecurityEmailTemplates.TwoFactorSettingsOtp(
                    code, recipient.GuardianLinkId != null ? user.UserName : null),
                HttpContext.RequestAborted,
                isHtml: true);
        }
        catch (Exception ex)
        {
            ClearPending();

            logger.LogWarning(
                "Unable to send 2FA settings OTP for {UserId}. " +
                "Error: {ErrorType}",
                user.Id,
                ex.GetType().Name);

            TempData["TwoFactorError"] =
                "Unable to send verification code. " +
                "Please try again later.";

            return RedirectToAction(nameof(TwoFactor));
        }

        TempData["TwoFactorSuccess"] =
            recipient.GuardianLinkId != null
                ? "A six-digit verification code was sent to your primary guardian."
                : "A six-digit verification code was sent to your registered email address.";

        return RedirectToAction(nameof(TwoFactor));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmChange(
        ConfirmChangeInput input)
    {
        var user = await GetUsableUserAsync();

        if (user == null)
            return Forbid();

        if (!AllowOperation(user, "verify"))
            return RedirectToAction(nameof(TwoFactor));

        var recipient = await recipients.ResolveAsync(
            user, requireVerifiedOwnEmail: true, HttpContext.RequestAborted);
        var pending = await GetValidPendingAsync(user, recipient);

        if (pending == null)
        {
            TempData["TwoFactorError"] =
                "The verification request has expired. " +
                "Please start again.";

            return RedirectToAction(nameof(TwoFactor));
        }

        if (!ModelState.IsValid)
        {
            TempData["TwoFactorError"] =
                "Enter a valid six-digit verification code.";

            return RedirectToAction(nameof(TwoFactor));
        }

        bool matches =
            CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(input.Code),
                Encoding.ASCII.GetBytes(pending.Code));

        if (!matches)
        {
            int remaining =
                pending.RemainingAttempts - 1;

            if (remaining <= 0)
            {
                ClearPending();

                TempData["TwoFactorError"] =
                    "Too many incorrect attempts. " +
                    "Please start again.";
            }
            else
            {
                SavePending(pending with
                {
                    RemainingAttempts = remaining
                });

                TempData["TwoFactorError"] =
                    $"Incorrect code. {remaining} attempt(s) left.";
            }

            return RedirectToAction(nameof(TwoFactor));
        }

        bool current =
            await users.GetTwoFactorEnabledAsync(user);

        if (current == pending.Enable)
        {
            ClearPending();

            TempData["TwoFactorInfo"] =
                "The requested setting is already applied.";

            return RedirectToAction(nameof(TwoFactor));
        }

        var result =
            await users.SetTwoFactorEnabledAsync(
                user,
                pending.Enable);

        if (!result.Succeeded)
        {
            ClearPending();

            TempData["TwoFactorError"] =
                "Unable to update the 2FA setting.";

            return RedirectToAction(nameof(TwoFactor));
        }

        ClearPending();

        // Invalidate older sessions, then renew this browser's cookie with the new stamp.
        var stampResult = await users.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await signIn.SignOutAsync();
            TempData["TwoFactorError"] =
                "Your 2FA preference was updated, but session security could not be refreshed. Please sign in again.";
            logger.LogWarning("Unable to renew session security after account {UserId} changed Email 2FA.", user.Id);
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        // Uses SecurityClaimsPrincipalFactory, which supplies a fresh LastActivityUtc claim.
        await signIn.RefreshSignInAsync(user);
        TempData["TwoFactorSuccess"] = pending.Enable
            ? "Email 2FA has been enabled successfully."
            : "Email 2FA has been disabled successfully.";

        logger.LogInformation(
            "Account {UserId} changed Email 2FA to {Enabled}.", user.Id, pending.Enable);
        return RedirectToAction(nameof(TwoFactor));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelChange()
    {
        ClearPending();

        TempData["TwoFactorInfo"] =
            "Your 2FA change request was cancelled.";

        return RedirectToAction(nameof(TwoFactor));
    }

    private async Task<ApplicationUser?> GetUsableUserAsync()
    {
        var user = await users.GetUserAsync(User);

        if (user == null ||
            !user.IsActive ||
            user.MustChangePassword ||
            await users.IsLockedOutAsync(user))
        {
            return null;
        }

        return user;
    }

    private async Task<PendingChange?> GetValidPendingAsync(
        ApplicationUser user,
        EmailOtpRecipient? recipient)
    {
        var pending = ReadPending();

        if (pending == null)
            return null;

        if (pending.UserId != user.Id ||
            recipient == null || pending.Recipient != recipient ||
            pending.RemainingAttempts <= 0 ||
            DateTimeOffset.UtcNow - pending.IssuedAt >= CodeLifetime ||
            pending.SecurityStamp !=
                await users.GetSecurityStampAsync(user))
        {
            ClearPending();
            return null;
        }

        return pending;
    }

    private PendingChange? ReadPending()
    {
        var value =
            HttpContext.Session.GetString(SessionKey);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingChange>(
                protector.Unprotect(value));
        }
        catch (Exception ex) when (
            ex is CryptographicException or JsonException)
        {
            ClearPending();
            return null;
        }
    }

    private void SavePending(PendingChange pending)
    {
        string protectedValue =
            protector.Protect(
                JsonSerializer.Serialize(pending));

        HttpContext.Session.SetString(
            SessionKey,
            protectedValue);
    }

    private void ClearPending()
    {
        HttpContext.Session.Remove(SessionKey);
    }
}

