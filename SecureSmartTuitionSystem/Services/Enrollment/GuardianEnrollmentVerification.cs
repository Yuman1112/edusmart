using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;

namespace SecureSmartTuitionSystem.Services.Enrollment;

// Separate from login OTP: grants consent to one enrollment draft, never signs in.
public sealed class GuardianEnrollmentVerification(ApplicationDbContext db,
    IDataProtectionProvider protection, AccountSecurityMail mail,
    ILogger<GuardianEnrollmentVerification> logger)
{
    public const string Provider = "EduSmart.EnrollmentConsent";
    private const string ChallengeName = "Challenge";
    private const string PendingKey = "Enrollment.GuardianOtp.Pending";
    private const string ResendKey = "Enrollment.GuardianOtp.ResendAt";
    private const string ProofKey = "Enrollment.GuardianOtp.Proof";
    private readonly IDataProtector protector = protection.CreateProtector("EduSmart.EnrollmentConsent.v1");
    private sealed record Challenge(string Id, string Hash, string Email, string Stamp,
        string Draft, DateTimeOffset Issued, int Attempts);
    private sealed record Pending(string UserId, string Id);
    private sealed record Proof(string UserId, string Email, string Stamp, string Draft, DateTimeOffset Expires);
    public long GetResendAtUnixSeconds(HttpContext context) =>
        long.TryParse(context.Session.GetString(ResendKey), out var value) ? value : 0;
    private void SetResendAt(HttpContext context, DateTimeOffset issued) =>
        context.Session.SetString(ResendKey, issued.AddMinutes(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
    public bool HasPending(HttpContext context) => context.Session.GetString(PendingKey) != null;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Draft(HttpContext context) => Hash(context.Session.GetString("Enrollment.StudentDetails") ?? "");
    private T? Read<T>(string? value) where T : class
    {
        try { return value == null ? null : JsonSerializer.Deserialize<T>(protector.Unprotect(value)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }
    private bool Eligible(ApplicationUser user) => user.IsActive && !user.MustChangePassword
        && !string.IsNullOrWhiteSpace(user.Email)
        && (!user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow);
    private Task<bool> IsParent(string id) => db.UserRoles.AnyAsync(r => r.UserId == id &&
        db.Roles.Any(role => role.Id == r.RoleId && role.Name == "Parent"));
    public void Clear(HttpContext context)
    {
        context.Session.Remove(PendingKey);
        context.Session.Remove(ProofKey);
    }
    public async Task<string?> SendAsync(HttpContext context, string? email, string? last4)
    {
        context.Session.Remove(ProofKey);
        context.Session.Remove("Enrollment.ExistingParentUserId");
        string normalized = (email ?? "").Trim().Normalize().ToUpperInvariant();
        last4 = last4?.Trim();
        if (normalized.Length == 0 || !System.Text.RegularExpressions.Regex.IsMatch(last4 ?? "", "^[0-9]{4}$"))
            return "Please check your registered email and the last 4 digits of your IC, including any leading zero. If both are correct, contact EduSmart support.";
        var matches = await db.ParentProfiles.Include(p => p.ApplicationUser)
            .Where(p => p.ApplicationUser.NormalizedEmail == normalized && p.IdentificationLast4 == last4)
            .Take(2).ToListAsync();
        bool eligible = matches.Count == 1 && Eligible(matches[0].ApplicationUser);
        bool parentRole = matches.Count == 1 && await IsParent(matches[0].ApplicationUserId);
        if (!eligible || !parentRole)
        {
            logger.LogWarning("Enrollment guardian lookup rejected. Matches: {Count}; Eligible: {Eligible}; ParentRole: {ParentRole}; Trace: {TraceId}",
                matches.Count, eligible, parentRole, context.TraceIdentifier);
            return "Please check your registered email and the last 4 digits of your IC, including any leading zero. If both are correct, contact EduSmart support.";
        }
        var user = matches[0].ApplicationUser;
        string code = RandomNumberGenerator.GetInt32(1000000).ToString("D6", CultureInfo.InvariantCulture);
        var challenge = new Challenge(Guid.NewGuid().ToString("N"), "", user.Email!, user.SecurityStamp ?? "",
            Draft(context), DateTimeOffset.UtcNow, 5);
        challenge = challenge with { Hash = Hash(challenge.Id + code) };
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var token = await db.UserTokens.SingleOrDefaultAsync(t => t.UserId == user.Id && t.LoginProvider == Provider && t.Name == ChallengeName);
            var old = Read<Challenge>(token?.Value);
            if (old != null && challenge.Issued - old.Issued < TimeSpan.FromMinutes(1))
            {
                SetResendAt(context, old.Issued);
                return "Please wait one minute before requesting another code.";
            }
            if (token == null) { token = new IdentityUserToken<string> { UserId = user.Id, LoginProvider = Provider, Name = ChallengeName }; db.UserTokens.Add(token); }
            token.Value = protector.Protect(JsonSerializer.Serialize(challenge));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        SetResendAt(context, challenge.Issued);
        context.Session.Remove(PendingKey);
        try
        {
            await mail.SendAsync(user.Email!, "EduSmart enrollment verification code",
                SecurityEmailTemplates.EnrollmentVerification(code), context.RequestAborted, isHtml: true);
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Enrollment verification email could not be sent.");
            return "We couldn't send the verification email. Please wait one minute and try again.";
        }
        context.Session.SetString(PendingKey, protector.Protect(JsonSerializer.Serialize(new Pending(user.Id, challenge.Id))));
        return null;
    }
    public async Task<bool> VerifyAsync(HttpContext context, string? code)
    {
        code = code?.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code ?? "", "^[0-9]{6}$")) return false;
        var pending = Read<Pending>(context.Session.GetString(PendingKey));
        if (pending == null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var token = await db.UserTokens.SingleOrDefaultAsync(t => t.UserId == pending.UserId && t.LoginProvider == Provider && t.Name == ChallengeName);
        var challenge = Read<Challenge>(token?.Value);
        if (challenge == null || challenge.Id != pending.Id || challenge.Attempts <= 0
            || DateTimeOffset.UtcNow - challenge.Issued > TimeSpan.FromMinutes(5) || challenge.Draft != Draft(context)) return false;
        challenge = challenge with { Attempts = challenge.Attempts - 1 };
        token!.Value = protector.Protect(JsonSerializer.Serialize(challenge));
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == pending.UserId);
        bool valid = user != null && Eligible(user) && await IsParent(user.Id)
            && user.Email == challenge.Email && (user.SecurityStamp ?? "") == challenge.Stamp
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(challenge.Id + (code ?? "").Trim())), Encoding.ASCII.GetBytes(challenge.Hash));
        if (valid)
        {
            user!.EmailConfirmed = true; // The submitted code proves control of this exact registered email.
            db.UserTokens.Remove(token);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (!valid) return false;
        context.Session.SetString(ProofKey, protector.Protect(JsonSerializer.Serialize(
            new Proof(user!.Id, challenge.Email, challenge.Stamp, challenge.Draft, DateTimeOffset.UtcNow.AddMinutes(30)))));
        context.Session.Remove(PendingKey);
        context.Session.SetString("Enrollment.ExistingParentUserId", user.Id);
        return true;
    }
    public async Task<ParentProfile?> GetVerifiedAsync(HttpContext context)
    {
        var proof = Read<Proof>(context.Session.GetString(ProofKey));
        if (proof == null || proof.Expires <= DateTimeOffset.UtcNow || proof.Draft != Draft(context)) return null;
        var parent = await db.ParentProfiles.AsNoTracking().Include(p => p.ApplicationUser).SingleOrDefaultAsync(p => p.ApplicationUserId == proof.UserId);
        if (parent == null || !Eligible(parent.ApplicationUser) || !await IsParent(proof.UserId)
            || parent.ApplicationUser.Email != proof.Email || (parent.ApplicationUser.SecurityStamp ?? "") != proof.Stamp) return null;
        return parent;
    }
    public static string ReceiptName(int applicationId) => "Application:" + applicationId;
    public static string ReceiptValue(EnrollmentApplication app) => Hash(JsonSerializer.Serialize(new {
        app.EnrollmentApplicationId, app.ExistingParentProfileId,
        Email = (app.GuardianEmail ?? "").Trim().Normalize().ToUpperInvariant(), app.StudentIdentificationNumber
    }));
    public void RecordConsent(EnrollmentApplication app, string parentUserId)
    {
        // Existing Identity token table gives durable, server-only consent without changing schema.
        db.UserTokens.Add(new IdentityUserToken<string> { UserId = parentUserId, LoginProvider = Provider,
            Name = ReceiptName(app.EnrollmentApplicationId), Value = ReceiptValue(app) });
    }
}

