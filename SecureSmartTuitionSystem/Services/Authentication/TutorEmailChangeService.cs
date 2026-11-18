using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication
{
    public sealed class TutorEmailChangeService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly ITutorEmailChangeEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TutorEmailChangeService> _logger;

        public TutorEmailChangeService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> users,
            ITutorEmailChangeEmailService emailService,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<TutorEmailChangeService> logger)
        {
            _db = db;
            _users = users;
            _emailService = emailService;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        // Returns null when successful, otherwise an error message.
        // The Controller owns and commits the transaction.
        public async Task<string?> PrepareAsync(
            ApplicationUser tutor,
            string newEmail,
            string adminUserId)
        {
            if (_db.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException(
                    "An active transaction is required.");
            }

            newEmail = (newEmail ?? "").Trim();

            if (string.IsNullOrWhiteSpace(newEmail)
                || newEmail.Length > 254
                || !new EmailAddressAttribute().IsValid(newEmail))
            {
                return "Please enter a valid email address.";
            }

            if (!tutor.IsActive
                || !await _users.IsInRoleAsync(tutor, "Tutor")
                || !await _db.TutorProfiles.AnyAsync(p => p.ApplicationUserId == tutor.Id && p.IsActive))
            {
                return "The active tutor account was not found.";
            }

            // The caller passes the currently signed-in user's ID.
            var requester = await _users.FindByIdAsync(adminUserId);

            if (requester == null || !requester.IsActive || requester.MustChangePassword || await _users.IsLockedOutAsync(requester))
            {
                return "You are not authorised to change this email.";
            }

            bool isAdmin =
                await _users.IsInRoleAsync(requester, "Admin");

            bool isOwnTutorAccount =
                requester.Id == tutor.Id
                && !requester.MustChangePassword
                && await _users.IsInRoleAsync(requester, "Tutor");

            if (!isAdmin && !isOwnTutorAccount)
            {
                return "You are not authorised to change this email.";
            }

            try
            {
                GetPublicOrigin();
            }
            catch (InvalidOperationException)
            {
                return "The public website address is not configured correctly.";
            }

            string? normalizedEmail =
                _users.NormalizeEmail(newEmail);

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return "Please enter a valid email address.";
            }

            if (normalizedEmail == tutor.NormalizedEmail) return "Please enter a different email address.";

            bool emailAlreadyUsed = await _db.Users.AnyAsync(user =>
                user.Id != tutor.Id
                && user.NormalizedEmail == normalizedEmail);

            if (emailAlreadyUsed)
            {
                return "This email is already used by another account. "
                    + "Please use a different email for the Tutor.";
            }

            await _db.Set<EmailChangeRequest>().Where(r => r.UserId == tutor.Id &&
                (r.Status == EmailChangeStatus.VerifyCurrent || r.Status == EmailChangeStatus.VerifyNew))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, EmailChangeStatus.Cancelled));

            DateTime now = DateTime.UtcNow;

            IdentityResult result;

            if (tutor.MustChangePassword)
            {
                // Revoke setup links previously sent to the old email.
                result = await _users.UpdateSecurityStampAsync(tutor);
            }
            else
            {
                result = await _users.UpdateAsync(tutor);
            }

            if (!result.Succeeded) return "Unable to save the email change request. Reload and try again.";
            _db.Add(new EmailChangeRequest {
                Id = Guid.NewGuid(), UserId = tutor.Id, NewEmail = newEmail,
                OriginalEmail = tutor.Email, RequestedByUserId = requester.Id,
                Purpose = EmailChangePurpose.TutorManaged,
                Status = EmailChangeStatus.VerifyNew, RequestedAtUtc = now,
                ExpiresAtUtc = now.AddHours(2), CodeExpiresAtUtc = now,
                SecurityStampHash = EmailChangeRules.Hash(tutor.SecurityStamp ?? ""),
                RecipientHash = "", CodeHash = "", CodeNonce = Guid.NewGuid(), LinkNonce = Guid.NewGuid()
            });
            await _db.SaveChangesAsync();
            return null;
        }

        public async Task<bool> SendPendingAsync(string tutorUserId)
        {
            try
            {
                var tutor = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Id == tutorUserId);

                var pending = await PendingAsync(tutorUserId);
                if (tutor == null || pending == null || await CurrentAsync(tutor, pending.Id) == null)
                    return false;

                string token =
                    await _users.GenerateChangeEmailTokenAsync(
                        tutor,
                        pending.NewEmail);

                string encodedToken = WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(token));

                string verificationUrl = QueryHelpers.AddQueryString(
                    GetPublicOrigin()
                        + "/TutorManagement/ConfirmEmailChange",
                    new Dictionary<string, string?>
                    {
                        ["userId"] = tutor.Id,
                        ["requestId"] =
                            pending.Id
                                .ToString("D"),
                        ["token"] = encodedToken
                    });

                await _emailService.SendAsync(
                    recipientEmail: pending.NewEmail,
                    tutorName: tutor.FullName ?? "",
                    tutorUserId: tutor.UserName ?? "",
                    verificationUrl: verificationUrl);

                return true;
            }
            catch (Exception ex)
            {
                // Never log tokens, links or SMTP credentials.
                _logger.LogWarning(
                    "Tutor email verification delivery failed for "
                    + "{UserId}. Error type: {ErrorType}.",
                    tutorUserId,
                    ex.GetType().Name);

                return false;
            }
        }

        public Task<EmailChangeRequest?> PendingAsync(string userId, bool tracking = false)
        {
            IQueryable<EmailChangeRequest> query = _db.Set<EmailChangeRequest>();
            if (!tracking) query = query.AsNoTracking();
            return query.Where(r => r.UserId == userId && r.Purpose == EmailChangePurpose.TutorManaged
                && r.Status == EmailChangeStatus.VerifyNew).OrderByDescending(r => r.RequestedAtUtc).FirstOrDefaultAsync();
        }

        public async Task<EmailChangeRequest?> CurrentAsync(ApplicationUser? tutor, Guid id, bool tracking = false)
        {
            if (tutor == null || !tutor.IsActive || id == Guid.Empty || !await _users.IsInRoleAsync(tutor, "Tutor")) return null;
            var row = await PendingAsync(tutor.Id, tracking);
            if (row == null || row.Id != id || row.ExpiresAtUtc <= DateTime.UtcNow ||
                row.SecurityStampHash != EmailChangeRules.Hash(tutor.SecurityStamp ?? "") ||
                !string.Equals(row.OriginalEmail, tutor.Email, StringComparison.OrdinalIgnoreCase) ||
                !await _db.TutorProfiles.AnyAsync(p => p.ApplicationUserId == tutor.Id && p.IsActive)) return null;
            return row;
        }

        private string GetPublicOrigin()
        {
            string? configuredUrl =
                _configuration["App:PublicBaseUrl"];

            if (string.IsNullOrWhiteSpace(configuredUrl)
                && _environment.IsDevelopment())
            {
                configuredUrl = "https://localhost:7086";
            }

            if (!Uri.TryCreate(
                    configuredUrl,
                    UriKind.Absolute,
                    out var baseUri)
                || baseUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(baseUri.UserInfo)
                || baseUri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(baseUri.Query)
                || !string.IsNullOrEmpty(baseUri.Fragment)
                || (!_environment.IsDevelopment() && baseUri.IsLoopback))
            {
                throw new InvalidOperationException(
                    "App:PublicBaseUrl must be the public HTTPS origin.");
            }

            return baseUri.GetLeftPart(UriPartial.Authority);
        }
    }
}