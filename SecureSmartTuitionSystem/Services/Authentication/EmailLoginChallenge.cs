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
    ILogger<EmailLoginChallenge> logger)
{
    private const string Provider = "EduSmart";
    private const string TokenName = "EmailLoginChallenge";
    private const string SessionKey = "EduSmart.EmailLoginChallenge";
    private readonly IDataProtector protector = protection.CreateProtector("EduSmart.EmailLoginChallenge.v1");

    private sealed record Challenge(string Id, string Code, string Stamp, DateTimeOffset Issued, int Remaining);
    private sealed record BrowserChallenge(string UserId, string Id, string Destination);

    public async Task<string?> StartAsync(HttpContext context, ApplicationUser user, string destination)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return "Your account needs a registered email address. Contact your administrator.";

        var now = DateTimeOffset.UtcNow;
        var previous = await ReadAsync(user);
        if (previous != null && now - previous.Issued < TimeSpan.FromMinutes(1))
            return "Please wait one minute before requesting another code.";

        var challenge = new Challenge(Guid.NewGuid().ToString("N"),
            RandomNumberGenerator.GetInt32(1000000).ToString("D6", CultureInfo.InvariantCulture),
            await users.GetSecurityStampAsync(user), now, 5);

        try
        {
            if (!(await StoreAsync(user, challenge)).Succeeded)
                return "The code could not be created. Please try signing in again.";

            await mail.SendAsync(
                user.Email,
                "EduSmart - Your Sign-in Verification Code",
                SecurityEmailTemplates.LoginOtp(challenge.Code),
                context.RequestAborted,
                isHtml: true);
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException
            or FormatException or ArgumentException or DbUpdateException)
        {
            logger.LogWarning("Sign-in code could not be issued ({ErrorType}).", ex.GetType().Name);
            return "We could not send a code. Please try again in one minute or contact your administrator.";
        }

        context.Session.SetString(SessionKey, JsonSerializer.Serialize(new BrowserChallenge(user.Id, challenge.Id, destination)));
        return null;
    }

    public bool IsPending(HttpContext context) => context.Session.GetString(SessionKey) != null;

    public async Task<(ApplicationUser? User, string Destination, string? Error)> VerifyAsync(HttpContext context, string code)
    {
        const string expired = "This code has expired or is no longer valid. Sign in again to request a new code.";
        var value = context.Session.GetString(SessionKey);
        if (value == null)
            return (null, "", expired);

        var browser = JsonSerializer.Deserialize<BrowserChallenge>(value)!;
        var user = await users.FindByIdAsync(browser.UserId);
        if (user == null || !user.IsActive || user.MustChangePassword || await users.IsLockedOutAsync(user))
            return (null, "", expired);

        var challenge = await ReadAsync(user);
        if (challenge == null || challenge.Id != browser.Id || challenge.Remaining < 1
            || DateTimeOffset.UtcNow - challenge.Issued >= TimeSpan.FromMinutes(5)
            || challenge.Stamp != await users.GetSecurityStampAsync(user))
            return (null, "", expired);

        bool matches = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code),
            Encoding.ASCII.GetBytes(challenge.Code));

        // Identity saves the token and the user concurrency stamp together. An overlapping
        // attempt that read the same stamp cannot also consume this code.
        var next = challenge with { Remaining = matches ? 0 : challenge.Remaining - 1 };
        if (!(await StoreAsync(user, next)).Succeeded)
            return (null, "", "The sign-in challenge changed. Please try again.");

        if (!matches)
            return (null, "", next.Remaining == 0 ? expired : $"Incorrect code. {next.Remaining} attempt(s) left.");

        return (user, browser.Destination, null);
    }

    private async Task<Challenge?> ReadAsync(ApplicationUser user)
    {
        var token = await users.GetAuthenticationTokenAsync(user, Provider, TokenName);
        if (token == null) return null;

        try
        {
            return JsonSerializer.Deserialize<Challenge>(protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private Task<IdentityResult> StoreAsync(ApplicationUser user, Challenge challenge) =>
        users.SetAuthenticationTokenAsync(user, Provider, TokenName, protector.Protect(JsonSerializer.Serialize(challenge)));
}
