using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed record EmailOtpRecipient(
    string Address,
    int? GuardianLinkId = null,
    string? GuardianUserId = null,
    string? GuardianSecurityStamp = null);

public sealed class EmailOtpRecipientResolver(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users)
{
    public async Task<EmailOtpRecipient?> ResolveAsync(
        ApplicationUser user,
        bool requireVerifiedOwnEmail,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return !requireVerifiedOwnEmail || user.EmailConfirmed
                ? new EmailOtpRecipient(user.Email)
                : null;
        }

        if (!await users.IsInRoleAsync(user, "Student"))
            return null;

        var primaryLinks = await db.ParentStudents.AsNoTracking()
            .Where(link => link.StudentProfile.ApplicationUserId == user.Id
                && link.StudentProfile.IsActive
                && link.IsActive && link.EndedAt == null && link.IsPrimaryGuardian)
            .Select(link => new
            {
                link.ParentStudentId,
                Guardian = link.ParentProfile.ApplicationUser
            })
            .Take(2)
            .ToListAsync(cancellationToken);

        // An ambiguous primary guardian assignment must be corrected before sending.
        if (primaryLinks.Count != 1)
            return null;

        var link = primaryLinks[0];
        var guardian = link.Guardian;
        if (guardian == null || !guardian.IsActive || guardian.MustChangePassword
            || !guardian.EmailConfirmed || string.IsNullOrWhiteSpace(guardian.Email)
            || !await users.IsInRoleAsync(guardian, "Parent"))
        {
            return null;
        }

        return new EmailOtpRecipient(
            guardian.Email, link.ParentStudentId, guardian.Id, guardian.SecurityStamp);
    }
}
