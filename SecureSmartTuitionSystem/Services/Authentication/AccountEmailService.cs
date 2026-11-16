using Microsoft.AspNetCore.DataProtection;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed record EmailOperation(bool Success, string Message);
public sealed class AccountEmailService(ApplicationDbContext db, UserManager<ApplicationUser> users,
    IEmailOtpRecipientResolver recipients, AccountEmailLinks links, AccountSecurityMail mail,
    IConfiguration configuration, IWebHostEnvironment environment, ILogger<AccountEmailService> logger, IDataProtectionProvider protection)
{
    private static readonly string[] Roles = ["Admin", "Tutor", "Parent", "Student"];
    public async Task<bool> UsableAsync(ApplicationUser? user) => user != null && user.IsActive && !user.MustChangePassword &&
        await users.HasPasswordAsync(user) && !(await users.IsLockedOutAsync(user)) && (await users.GetRolesAsync(user)).Any(Roles.Contains);
    public Task<EmailChangeRequest?> PendingAsync(string userId) => db.Set<EmailChangeRequest>().AsNoTracking()
        .Where(r => r.UserId == userId && (r.Status == EmailChangeStatus.VerifyCurrent || r.Status == EmailChangeStatus.VerifyNew))
        .OrderByDescending(r => r.RequestedAtUtc).FirstOrDefaultAsync();
    private static EmailOperation Error(string message) => new(false, message);
    private static void ClearLegacy(ApplicationUser user)
    {
        user.PendingTutorEmail = null; user.TutorEmailChangeRequestId = null; user.TutorEmailChangeRequestedAtUtc = null;
        user.TutorEmailChangeExpiresAtUtc = null; user.TutorEmailChangeRequestedByUserId = null;
    }
    private string SetCode(EmailChangeRequest row)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        row.CodeNonce = Guid.NewGuid(); row.CodeHash = protection.CreateProtector("EduSmart.EmailChange.Code.v1", row.Id.ToString("N")).Protect(EmailChangeRules.Hash(row.CodeNonce.ToString("N") + ":" + code));
        row.CodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(10); row.Attempts = 0; return code;
    }
    private string? ReadCodeHash(EmailChangeRequest row)
    {
        try { return protection.CreateProtector("EduSmart.EmailChange.Code.v1", row.Id.ToString("N")).Unprotect(row.CodeHash); }
        catch (CryptographicException) { return null; }
    }
    private string Origin()
    {
        var origin = configuration["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment()) origin = "https://localhost:7086";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || (!environment.IsDevelopment() && uri.IsLoopback))
            throw new InvalidOperationException("The public website address is not configured correctly.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
    private async Task LockAsync(string userId)
    {
        var resource = "EduSmart.AccountEmail:" + userId;
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @r < 0 THROW 50001, 'Please retry the account email operation.', 1;");
    }
    private async Task<bool> SendAsync(string address, string subject, string body)
    {
        try { await mail.SendAsync(address, subject, body, CancellationToken.None, isHtml: true); return true; }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException or FormatException or ArgumentException)
        { logger.LogWarning("Account email delivery failed ({ErrorType}).", ex.GetType().Name); return false; }
    }
    public async Task<EmailOperation> StartAsync(ApplicationUser user, string newEmail)
    {
        if (!await UsableAsync(user)) return Error("This account is not available for email changes.");
        newEmail = newEmail.Trim();
        if (newEmail.Length > 254 || !new EmailAddressAttribute().IsValid(newEmail)) return Error("Enter a valid email address.");
        if (users.NormalizeEmail(newEmail) == users.NormalizeEmail(user.Email)) return Error("Enter a different email address.");
        try { Origin(); } catch (InvalidOperationException) { return Error("The public website address is not configured correctly."); }
        var recipient = await recipients.ResolveAsync(user, true, CancellationToken.None);
        if (recipient == null) return Error("A verified current email or eligible primary guardian is required. Contact the tuition centre for help.");
        var row = new EmailChangeRequest { Id = Guid.NewGuid(), UserId = user.Id, NewEmail = newEmail,
            OriginalEmail = user.Email, SecurityStampHash = EmailChangeRules.Hash(user.SecurityStamp ?? ""),
            RecipientHash = EmailChangeRules.RecipientHash(recipient), RequestedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2), Status = EmailChangeStatus.VerifyCurrent };
        var code = SetCode(row);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await LockAsync(user.Id);
            var normalized = users.NormalizeEmail(newEmail);
            if (await db.Users.AnyAsync(u => u.Id != user.Id && u.NormalizedEmail == normalized)) return Error("This address cannot be used. Choose another email.");
            await db.Set<EmailChangeRequest>().Where(r => r.UserId == user.Id && (r.Status == EmailChangeStatus.VerifyCurrent || r.Status == EmailChangeStatus.VerifyNew))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, EmailChangeStatus.Cancelled));
            ClearLegacy(user);
            if (!(await users.UpdateAsync(user)).Succeeded) return Error("Your account changed. Reload and try again.");
            db.Add(row); await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        { db.ChangeTracker.Clear(); return Error("The email request could not be saved. Reload and try again."); }
        var sent = await SendAsync(recipient.Address, "EduSmart - Confirm email change", SecurityEmailTemplates.CurrentEmailCode(code, user.UserName ?? "", newEmail));
        return new(sent, sent ? "A code was sent to your current verified email or primary guardian. Enter it below." : "The request was saved, but the code could not be delivered. Use Resend after a short wait.");
    }
    private async Task<bool> EligibleAsync(EmailChangeRequest? row, ApplicationUser user)
    {
        if (row == null || !await UsableAsync(user) || !EmailChangeRules.Live(row, user, DateTime.UtcNow)) return false;
        var recipient = await recipients.ResolveAsync(user, true, CancellationToken.None);
        return recipient != null && row.RecipientHash == EmailChangeRules.RecipientHash(recipient);
    }
    public async Task<EmailOperation> VerifyCurrentAsync(ApplicationUser user, Guid id, string? code)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await LockAsync(user.Id);
            var row = await db.Set<EmailChangeRequest>().SingleOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
            if (!await EligibleAsync(row, user) || row!.Status != EmailChangeStatus.VerifyCurrent) return Error("This request is no longer valid. Start a new request.");
            if (!EmailChangeRules.CodeMatches(row, code?.Trim(), DateTime.UtcNow, ReadCodeHash(row)))
            {
                row.Attempts++; if (row.Attempts >= 5) row.Status = EmailChangeStatus.Cancelled;
                await db.SaveChangesAsync(); await tx.CommitAsync();
                return Error(row.Attempts >= 5 ? "Too many incorrect codes. Start a new request." : "The code is incorrect or expired.");
            }
            row.Status = EmailChangeStatus.VerifyNew; row.CodeHash = ""; row.LinkNonce = Guid.NewGuid();
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        { db.ChangeTracker.Clear(); return Error("The request changed. Reload and try again."); }
        return await SendNewAsync(user, id);
    }
    private async Task<EmailOperation> SendNewAsync(ApplicationUser user, Guid id)
    {
        var row = await db.Set<EmailChangeRequest>().SingleOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
        if (!await EligibleAsync(row, user) || row!.Status != EmailChangeStatus.VerifyNew) return Error("This request is no longer valid.");
        var token = await users.GenerateChangeEmailTokenAsync(user, row.NewEmail);
        var proof = links.ProtectChange(new(row.Id, row.LinkNonce, token), row.ExpiresAtUtc);
        string url;
        try { url = Origin() + "/AccountEmail/Confirm?token=" + Uri.EscapeDataString(proof); }
        catch (InvalidOperationException) { return Error("The public website address is not configured correctly."); }
        var sent = await SendAsync(row.NewEmail, "EduSmart - Verify new email", SecurityEmailTemplates.NewEmailVerification(url, user.UserName ?? ""));
        return new(sent, sent ? "Verification email sent to your new address. Your current email remains unchanged until you confirm." : "The verification email could not be delivered. Use Resend after a short wait.");
    }
    public async Task<EmailOperation> ResendAsync(ApplicationUser user, Guid id)
    {
        string? code = null; string? address = null; string? newEmail = null;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await LockAsync(user.Id);
            var row = await db.Set<EmailChangeRequest>().SingleOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
            if (!await EligibleAsync(row, user)) return Error("This request expired or changed. Cancel it and start a new request.");
            if (row!.Status == EmailChangeStatus.VerifyCurrent)
            {
                code = SetCode(row); address = (await recipients.ResolveAsync(user, true, CancellationToken.None))!.Address; newEmail = row.NewEmail;
            }
            else row.LinkNonce = Guid.NewGuid();
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        { db.ChangeTracker.Clear(); return Error("The request changed. Reload and try again."); }
        if (code == null) return await SendNewAsync(user, id);
        var sent = await SendAsync(address!, "EduSmart - Confirm email change", SecurityEmailTemplates.CurrentEmailCode(code, user.UserName ?? "", newEmail!));
        return new(sent, sent ? "A new code was sent. Earlier codes no longer work." : "The code could not be delivered. Try again later.");
    }
    public async Task<EmailOperation> CancelAsync(ApplicationUser user, Guid id)
    {
        try
        {
            var count = await db.Set<EmailChangeRequest>().Where(r => r.Id == id && r.UserId == user.Id && (r.Status == EmailChangeStatus.VerifyCurrent || r.Status == EmailChangeStatus.VerifyNew))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, EmailChangeStatus.Cancelled));
            return new(count == 1, count == 1 ? "Email change cancelled. Your current email is unchanged." : "This request is no longer pending.");
        }
        catch (DbException) { return Error("The request could not be cancelled. Try again."); }
    }
    public async Task<EmailChangeRequest?> ConfirmationAsync(string? token)
    {
        var proof = links.ReadChange(token); if (proof == null) return null;
        var row = await db.Set<EmailChangeRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == proof.RequestId);
        if (row == null || row.LinkNonce != proof.Nonce || row.Status != EmailChangeStatus.VerifyNew) return null;
        var user = await users.FindByIdAsync(row.UserId);
        return user != null && await EligibleAsync(row, user) ? row : null;
    }
    public async Task<(EmailOperation Result, string? UserId)> ConfirmAsync(string? token)
    {
        var proof = links.ReadChange(token); if (proof == null) return (Error("This link is invalid or expired."), null);
        string? notice = null; string? userName = null; string? owner = null;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var row = await db.Set<EmailChangeRequest>().SingleOrDefaultAsync(r => r.Id == proof.RequestId);
            if (row == null) return (Error("This link is invalid or expired."), null);
            await LockAsync(row.UserId);
            var user = await users.FindByIdAsync(row.UserId);
            if (user == null || row.LinkNonce != proof.Nonce || row.Status != EmailChangeStatus.VerifyNew || !await EligibleAsync(row, user))
                return (Error("This link is invalid, expired or replaced. Start again from your profile."), null);
            var normalized = users.NormalizeEmail(row.NewEmail);
            if (await db.Users.AnyAsync(u => u.Id != user.Id && u.NormalizedEmail == normalized)) return (Error("This address can no longer be used. Start a new request."), null);
            notice = (await recipients.ResolveAsync(user, true, CancellationToken.None))!.Address; userName = user.UserName; owner = user.Id;
            var result = await users.ChangeEmailAsync(user, row.NewEmail, proof.IdentityToken);
            if (!result.Succeeded) return (Error("The email could not be changed. Request a new verification link."), null);
            row.Status = EmailChangeStatus.Completed; row.CompletedAtUtc = DateTime.UtcNow; row.CodeHash = "";
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        { db.ChangeTracker.Clear(); return (Error("The request changed. Reload and try again."), null); }
        await SendAsync(notice!, "EduSmart - Account email updated", SecurityEmailTemplates.EmailChangeNotice(userName ?? ""));
        return (new(true, "Your email is verified and updated. Sign in again. Your User ID, password, 2FA setting and guardian links are unchanged."), owner);
    }
}
