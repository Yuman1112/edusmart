using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.Services.Enrollment;

/// <summary>Checks who may bind an application to an existing family account.
/// Run before requesting payment and again during paid fulfillment.</summary>
public sealed class GuardianAccountLinkPolicy(ApplicationDbContext db)
{
    public async Task<string?> ValidateAsync(EnrollmentApplication application,
        CancellationToken cancellationToken = default)
    {
        if (!application.ExistingParentProfileId.HasValue)
        {
            string? icError = GuardianIcValidation.GetError(application.GuardianIdentificationNumber);
            if (icError != null)
                return icError;
            if (await GuardianIcValidation.IsRegisteredAsync(
                    db, application.GuardianIdentificationNumber!, cancellationToken))
                return GuardianIcValidation.DuplicateMessage;

            string email = (application.GuardianEmail ?? "").Trim().Normalize().ToUpperInvariant();
            if (email.Length == 0 || await db.Users.AsNoTracking().AnyAsync(
                x => x.NormalizedEmail == email, cancellationToken))
                return "The guardian account needs review. Ask the guardian to sign in and submit the request using their existing account, or contact support.";
            return null;
        }

        var parent = await db.ParentProfiles.AsNoTracking()
            .Where(x => x.ParentProfileId == application.ExistingParentProfileId.Value)
            .Select(x => new { x.ParentProfileId, x.ApplicationUserId,
                x.ApplicationUser.IsActive, x.ApplicationUser.NormalizedEmail })
            .FirstOrDefaultAsync(cancellationToken);
        string emailSnapshot = (application.GuardianEmail ?? "").Trim().Normalize().ToUpperInvariant();
        if (parent == null || !parent.IsActive ||
            string.IsNullOrWhiteSpace(parent.NormalizedEmail) || parent.NormalizedEmail != emailSnapshot)
            return "The existing guardian account or email needs review. Ask the guardian to sign in and submit a new request.";

        // A verified enrollment-only OTP leaves a durable receipt for approval and fulfillment.
        string receiptName = GuardianEnrollmentVerification.ReceiptName(application.EnrollmentApplicationId);
        string receiptValue = GuardianEnrollmentVerification.ReceiptValue(application);
        if (await db.UserTokens.AsNoTracking().AnyAsync(t => t.UserId == parent.ApplicationUserId &&
            t.LoginProvider == GuardianEnrollmentVerification.Provider &&
            t.Name == receiptName &&
            t.Value == receiptValue, cancellationToken)
            && await db.UserRoles.AnyAsync(r => r.UserId == parent.ApplicationUserId &&
                db.Roles.Any(role => role.Id == r.RoleId && role.Name == "Parent"), cancellationToken)) return null;

        if (string.IsNullOrWhiteSpace(application.SubmittedByUserId))
            return "This application has no valid guardian consent. Please ask the guardian to submit a new enrollment and complete email verification.";
        if (parent.ApplicationUserId == application.SubmittedByUserId) return null;

        // Staff-assisted additions must refer to an existing linked student.
        // Prove the recorded submitter's active Admin role from the database,
        // not from a form field, email, or the later payment request's identity.
        if (application.ExistingStudentProfileId.HasValue &&
            await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == application.SubmittedByUserId &&
                db.Roles.Any(role => role.Id == r.RoleId && role.Name == "Admin") &&
                db.Users.Any(user => user.Id == r.UserId && user.IsActive), cancellationToken) &&
            await db.ParentStudents.AsNoTracking().AnyAsync(link => link.IsActive && link.StudentProfile.IsActive &&
                link.ParentProfileId == parent.ParentProfileId && link.StudentProfileId == application.ExistingStudentProfileId.Value, cancellationToken))
            return null;

        // A student may request another subject only for their own existing
        // profile and only through a currently active guardian relationship.
        if (application.ExistingStudentProfileId.HasValue &&
            await db.ParentStudents.AsNoTracking().AnyAsync(link => link.IsActive &&
                link.ParentProfileId == parent.ParentProfileId &&
                link.StudentProfileId == application.ExistingStudentProfileId.Value &&
                link.StudentProfile.IsActive &&
                link.StudentProfile.ApplicationUserId == application.SubmittedByUserId, cancellationToken))
            return null;

        return "The application is not linked to the signed-in guardian or an eligible linked student. Ask the family to sign in and submit a new request.";
    }
}
