
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed class EmailLoginChallenge(
    UserManager<ApplicationUser> users,
    IDataProtectionProvider protection,
    AccountSecurityMail mail,
    EmailOtpRecipientResolver recipients,
    ILogger<EmailLoginChallenge> logger)
{
    private const string Provider = "EduSmart";
    private const string TokenName = "EmailLoginChallenge";
    private const string SessionKey = "EduSmart.EmailLoginChallenge";

    private readonly IDataProtector protector =
        protection.CreateProtector(
            "EduSmart.EmailLoginChallenge.v1");

    private sealed record Challenge(
        string Id,
        string Code,
        string Stamp,
        EmailOtpRecipient Recipient,
        DateTimeOffset Issued,
        int Remaining);

    private sealed record BrowserChallenge(
        string UserId,
        string Id,
        string Destination);

    // =========================================================
    // START EMAIL LOGIN CHALLENGE
    // =========================================================
    public async Task<string?> StartAsync(
        HttpContext context,
        ApplicationUser user,
        string destination)
    {
        // Remove any previous pending browser challenge.
        context.Session.Remove(SessionKey);

        if (!user.IsActive ||
            user.MustChangePassword ||
            await users.IsLockedOutAsync(user))
        {
            return "This account is not available for sign-in.";
        }

        // Email OTP is only required when 2FA is enabled.
        if (!await users.GetTwoFactorEnabledAsync(user))
        {
            return "Email 2FA is not enabled for this account.";
        }

        // Keep sign-in available for existing accounts with an unconfirmed
        // personal address. Guardian delivery always requires verification.
        var recipient = await recipients.ResolveAsync(
            user, requireVerifiedOwnEmail: false, context.RequestAborted);
        if (recipient == null)
        {
            return "No eligible verification email is available. " +
                   "Contact your administrator for help with your account or guardian details.";
        }

        var now = DateTimeOffset.UtcNow;
        var previous = await ReadAsync(user);

        // Prevent repeated OTP requests within one minute.
        if (previous != null &&
            now - previous.Issued < TimeSpan.FromMinutes(1))
        {
            return "Please wait one minute before requesting another code.";
        }

        var challenge = new Challenge(
            Guid.NewGuid().ToString("N"),
            RandomNumberGenerator
                .GetInt32(1000000)
                .ToString("D6", CultureInfo.InvariantCulture),
            await users.GetSecurityStampAsync(user),
            recipient,
            now,
            5);

        try
        {
            var storeResult = await StoreAsync(user, challenge);

            if (!storeResult.Succeeded)
            {
                return "The code could not be created. " +
                       "Please try signing in again.";
            }

            await mail.SendAsync(
                recipient.Address,
                "EduSmart - Your Sign-in Verification Code",
                SecurityEmailTemplates.LoginOtp(
                    challenge.Code, recipient.GuardianLinkId != null ? user.UserName : null),
                context.RequestAborted,
                isHtml: true);
        }
        catch (Exception ex) when (
            ex is System.Net.Mail.SmtpException
            or InvalidOperationException
            or FormatException
            or ArgumentException
            or DbUpdateException)
        {
            logger.LogWarning(
                "Sign-in code could not be issued ({ErrorType}).",
                ex.GetType().Name);

            return "We could not send a code. " +
                   "Please try again in one minute " +
                   "or contact your administrator.";
        }

        context.Session.SetString(
            SessionKey,
            JsonSerializer.Serialize(
                new BrowserChallenge(
                    user.Id,
                    challenge.Id,
                    destination)));

        return null;
    }

    // =========================================================
    // CHECK WHETHER OTP VERIFICATION IS PENDING
    // =========================================================
    public bool IsPending(HttpContext context)
    {
        return context.Session.GetString(SessionKey) != null;
    }

    // =========================================================
    // CLEAR PENDING BROWSER CHALLENGE
    // =========================================================
    public void ClearPending(HttpContext context)
    {
        context.Session.Remove(SessionKey);
    }

    // =========================================================
    // VERIFY EMAIL LOGIN OTP
    // =========================================================
    public async Task<(
        ApplicationUser? User,
        string Destination,
        string? Error)> VerifyAsync(
            HttpContext context,
            string code)
    {
        const string expired =
            "This code has expired or is no longer valid. " +
            "Sign in again to request a new code.";

        var value = context.Session.GetString(SessionKey);

        if (value == null)
        {
            return (null, "", expired);
        }

        BrowserChallenge? browser;

        try
        {
            browser =
                JsonSerializer.Deserialize<BrowserChallenge>(value);
        }
        catch (JsonException)
        {
            ClearPending(context);
            return (null, "", expired);
        }

        if (browser == null ||
            string.IsNullOrWhiteSpace(browser.UserId) ||
            string.IsNullOrWhiteSpace(browser.Id))
        {
            ClearPending(context);
            return (null, "", expired);
        }

        var user = await users.FindByIdAsync(browser.UserId);

        // Recheck the latest account state.
        if (user == null ||
            !user.IsActive ||
            user.MustChangePassword ||
            !await users.GetTwoFactorEnabledAsync(user) ||
            await users.IsLockedOutAsync(user))
        {
            ClearPending(context);
            return (null, "", expired);
        }

        var challenge = await ReadAsync(user);

        if (challenge == null ||
            challenge.Id != browser.Id ||
            challenge.Remaining < 1 ||
            DateTimeOffset.UtcNow - challenge.Issued
                >= TimeSpan.FromMinutes(5) ||
            challenge.Stamp !=
                await users.GetSecurityStampAsync(user))
        {
            ClearPending(context);
            return (null, "", expired);
        }

        var recipient = await recipients.ResolveAsync(
            user, requireVerifiedOwnEmail: false, context.RequestAborted);
        if (recipient == null || recipient != challenge.Recipient)
        {
            ClearPending(context);
            return (null, "", expired);
        }

        // Reject invalid code formats before comparison.
        if (string.IsNullOrEmpty(code) ||
            code.Length != 6 ||
            !code.All(c => c >= '0' && c <= '9'))
        {
            return (null, "", "Enter a valid six-digit code.");
        }

        bool matches =
            CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(code),
                Encoding.ASCII.GetBytes(challenge.Code));

        // Consume the code on success.
        // Reduce the remaining attempts on failure.
        var next = challenge with
        {
            Remaining = matches
                ? 0
                : challenge.Remaining - 1
        };

        var storeResult = await StoreAsync(user, next);

        if (!storeResult.Succeeded)
        {
            ClearPending(context);

            return (
                null,
                "",
                "The sign-in challenge changed. Please try again.");
        }

        if (!matches)
        {
            if (next.Remaining == 0)
            {
                ClearPending(context);
                return (null, "", expired);
            }

            return (
                null,
                "",
                $"Incorrect code. {next.Remaining} attempt(s) left.");
        }

        // Successful OTP verification.
        ClearPending(context);

        return (user, browser.Destination, null);
    }

    // =========================================================
    // READ STORED OTP CHALLENGE
    // =========================================================
    private async Task<Challenge?> ReadAsync(
        ApplicationUser user)
    {
        var token =
            await users.GetAuthenticationTokenAsync(
                user,
                Provider,
                TokenName);

        if (token == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Challenge>(
                protector.Unprotect(token));
        }
        catch (Exception ex) when (
            ex is CryptographicException
            or JsonException)
        {
            return null;
        }
    }

    // =========================================================
    // STORE OTP CHALLENGE
    // =========================================================
    private Task<IdentityResult> StoreAsync(
        ApplicationUser user,
        Challenge challenge)
    {
        return users.SetAuthenticationTokenAsync(
            user,
            Provider,
            TokenName,
            protector.Protect(
                JsonSerializer.Serialize(challenge)));
    }
}
