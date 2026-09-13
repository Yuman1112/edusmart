using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication
{
    public class AccountProvisioningService
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;

        public AccountProvisioningService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<AccountProvisioningResult>
            ProvisionEnrollmentAsync(
                int enrollmentApplicationId,
                CancellationToken cancellationToken = default)
        {
            if (enrollmentApplicationId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enrollmentApplicationId));
            }

            var application =
                await _context.EnrollmentApplications
                    .Include(item =>
                        item.SelectedSubjects)
                    .FirstOrDefaultAsync(
                        item =>
                            item.EnrollmentApplicationId ==
                            enrollmentApplicationId,
                        cancellationToken);

            if (application == null)
            {
                throw new InvalidOperationException(
                    "The enrollment application was not found.");
            }

            if (application.SelectedSubjects.Count == 0)
            {
                throw new InvalidOperationException(
                    "The enrollment application has no classes.");
            }

            ParentProfile parentProfile =
                await GetOrCreateParentProfileAsync(
                    application,
                    cancellationToken);

            StudentProfile studentProfile =
                await GetOrCreateStudentProfileAsync(
                    application,
                    parentProfile,
                    cancellationToken);

            int createdEnrollmentCount =
                await CreateStudentEnrollmentsAsync(
                    application,
                    studentProfile,
                    cancellationToken);

            application.ExistingParentProfileId =
                parentProfile.ParentProfileId;

            application.ExistingStudentProfileId =
                studentProfile.StudentProfileId;

            await _context.SaveChangesAsync(
                cancellationToken);

            return new AccountProvisioningResult
            {
                ParentProfileId =
                    parentProfile.ParentProfileId,

                ParentUserId =
                    parentProfile.ApplicationUserId,

                ParentUserName =
                    parentProfile.ApplicationUser.UserName
                    ?? string.Empty,

                StudentProfileId =
                    studentProfile.StudentProfileId,

                StudentUserId =
                    studentProfile.ApplicationUserId
                    ?? string.Empty,

                StudentUserName =
                    studentProfile.ApplicationUser?.UserName
                    ?? string.Empty,

                CreatedEnrollmentCount =
                    createdEnrollmentCount
            };
        }

        public async Task<string> GenerateNextUserIdAsync(
            string prefix,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prefix) ||
                prefix.Length != 2)
            {
                throw new ArgumentException(
                    "The user ID prefix must contain two characters.",
                    nameof(prefix));
            }

            prefix =
                prefix.ToUpperInvariant();

            var userNames =
                await _context.Users
                    .AsNoTracking()
                    .Where(user =>
                        user.UserName != null &&
                        user.UserName.StartsWith(prefix))
                    .Select(user =>
                        user.UserName!)
                    .ToListAsync(
                        cancellationToken);

            int highestNumber = 0;

            foreach (string userName in userNames)
            {
                if (userName.Length != 7)
                {
                    continue;
                }

                string numericPart =
                    userName.Substring(2);

                if (int.TryParse(
                        numericPart,
                        out int number) &&
                    number > highestNumber)
                {
                    highestNumber = number;
                }
            }

            int nextNumber =
                highestNumber + 1;

            if (nextNumber > 99999)
            {
                throw new InvalidOperationException(
                    $"The maximum {prefix} user ID has been reached.");
            }

            return $"{prefix}{nextNumber:D5}";
        }

        private async Task<ParentProfile>
            GetOrCreateParentProfileAsync(
                EnrollmentApplication application,
                CancellationToken cancellationToken)
        {
            /*
             * Use the existing parent profile already linked
             * during application submission.
             */
            if (application.ExistingParentProfileId.HasValue)
            {
                var linkedProfile =
                    await _context.ParentProfiles
                        .Include(profile =>
                            profile.ApplicationUser)
                        .FirstOrDefaultAsync(
                            profile =>
                                profile.ParentProfileId ==
                                application
                                    .ExistingParentProfileId
                                    .Value,
                            cancellationToken);

                if (linkedProfile == null)
                {
                    throw new InvalidOperationException(
                        "The linked parent profile was not found.");
                }

                return linkedProfile;
            }

            /*
             * Try to reuse an existing Parent account
             * with the same email address.
             */
            var existingUser =
                await _userManager.FindByEmailAsync(
                    application.GuardianEmail);

            if (existingUser != null)
            {
                bool isParent =
                    await _userManager.IsInRoleAsync(
                        existingUser,
                        "Parent");

                if (!isParent)
                {
                    throw new InvalidOperationException(
                        "The guardian email belongs to a " +
                        "non-parent system account.");
                }

                var existingProfile =
                    await _context.ParentProfiles
                        .Include(profile =>
                            profile.ApplicationUser)
                        .FirstOrDefaultAsync(
                            profile =>
                                profile.ApplicationUserId ==
                                existingUser.Id,
                            cancellationToken);

                if (existingProfile != null)
                {
                    return existingProfile;
                }

                var missingProfile =
                    new ParentProfile
                    {
                        ApplicationUserId =
                            existingUser.Id,

                        FullName =
                            application.GuardianName,

                        IdentificationNumber =
                            application
                                .GuardianIdentificationNumber,

                        IdentificationLast4 =
                            application
                                .GuardianIdentificationLast4,

                        CreatedAt =
                            DateTime.UtcNow,

                        ApplicationUser =
                            existingUser
                    };

                _context.ParentProfiles.Add(
                    missingProfile);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return missingProfile;
            }

            /*
             * Create a new passwordless Parent account.
             * A separate account setup flow will be used
             * for the first password.
             */
            string parentUserName =
                await GenerateNextUserIdAsync(
                    "PA",
                    cancellationToken);

            var parentUser =
                new ApplicationUser
                {
                    UserName =
                        parentUserName,

                    Email =
                        application.GuardianEmail,

                    PhoneNumber =
                        application.GuardianPhoneNumber,

                    FullName =
                        application.GuardianName,

                    IsActive =
                        true,

                    LockoutEnabled =
                        true,

                    EmailConfirmed =
                        false,

                    MustChangePassword =
                        true,

                    TwoFactorEnabled =
                        false,

                    CreatedAt =
                        DateTime.UtcNow
                };

            IdentityResult createResult =
                await _userManager.CreateAsync(
                    parentUser);

            EnsureIdentitySucceeded(
                createResult,
                "Unable to create the parent account.");

            IdentityResult roleResult =
                await _userManager.AddToRoleAsync(
                    parentUser,
                    "Parent");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(
                    parentUser);

                EnsureIdentitySucceeded(
                    roleResult,
                    "Unable to assign the Parent role.");
            }

            var parentProfile =
                new ParentProfile
                {
                    ApplicationUserId =
                        parentUser.Id,

                    FullName =
                        application.GuardianName,

                    IdentificationNumber =
                        application
                            .GuardianIdentificationNumber,

                    IdentificationLast4 =
                        application
                            .GuardianIdentificationLast4,

                    CreatedAt =
                        DateTime.UtcNow,

                    ApplicationUser =
                        parentUser
                };

            _context.ParentProfiles.Add(
                parentProfile);

            await _context.SaveChangesAsync(
                cancellationToken);

            return parentProfile;
        }

        private async Task<StudentProfile>
            GetOrCreateStudentProfileAsync(
                EnrollmentApplication application,
                ParentProfile parentProfile,
                CancellationToken cancellationToken)
        {
            /*
             * Use the existing student profile already linked
             * during application submission.
             */
            if (application.ExistingStudentProfileId.HasValue)
            {
                var linkedProfile =
                    await _context.StudentProfiles
                        .Include(profile =>
                            profile.ApplicationUser)
                        .FirstOrDefaultAsync(
                            profile =>
                                profile.StudentProfileId ==
                                application
                                    .ExistingStudentProfileId
                                    .Value,
                            cancellationToken);

                if (linkedProfile == null)
                {
                    throw new InvalidOperationException(
                        "The linked student profile was not found.");
                }

                if (linkedProfile.ParentProfileId !=
                    parentProfile.ParentProfileId)
                {
                    throw new InvalidOperationException(
                        "The linked student belongs to a " +
                        "different parent profile.");
                }

                return linkedProfile;
            }

            ValidateStudentInformation(application);

            /*
             * Reuse an exact identification-number match.
             */
            var existingStudentProfile =
                await _context.StudentProfiles
                    .Include(profile =>
                        profile.ApplicationUser)
                    .FirstOrDefaultAsync(
                        profile =>
                            profile.IdentificationNumber ==
                            application
                                .StudentIdentificationNumber,
                        cancellationToken);

            if (existingStudentProfile != null)
            {
                if (existingStudentProfile.ParentProfileId !=
                    parentProfile.ParentProfileId)
                {
                    throw new InvalidOperationException(
                        "A student with the same identification " +
                        "number belongs to another parent profile.");
                }

                return existingStudentProfile;
            }

            string studentUserName =
                await GenerateNextUserIdAsync(
                    "ST",
                    cancellationToken);

            /*
             * Student email is optional because younger
             * students may log in using ST00001.
             */
            var studentUser =
                new ApplicationUser
                {
                    UserName =
                        studentUserName,

                    Email =
                        null,

                    FullName =
                        application.StudentName,

                    IsActive =
                        true,

                    LockoutEnabled =
                        true,

                    EmailConfirmed =
                        false,

                    MustChangePassword =
                        true,

                    TwoFactorEnabled =
                        false,

                    CreatedAt =
                        DateTime.UtcNow
                };

            IdentityResult createResult =
                await _userManager.CreateAsync(
                    studentUser);

            EnsureIdentitySucceeded(
                createResult,
                "Unable to create the student account.");

            IdentityResult roleResult =
                await _userManager.AddToRoleAsync(
                    studentUser,
                    "Student");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(
                    studentUser);

                EnsureIdentitySucceeded(
                    roleResult,
                    "Unable to assign the Student role.");
            }

            var studentProfile =
                new StudentProfile
                {
                    StudentNumber =
                        studentUserName,

                    FullName =
                        application.StudentName,

                    IdentificationNumber =
                        application.StudentIdentificationNumber!,

                    DateOfBirth =
                        application.StudentDateOfBirth,

                    Gender =
                        application.StudentGender!,

                    School =
                        application.StudentSchool!,

                    AcademicLevel =
                        application.AcademicLevel,

                    IsActive =
                        true,

                    CreatedAt =
                        DateTime.UtcNow,

                    ParentProfileId =
                        parentProfile.ParentProfileId,

                    ParentProfile =
                        parentProfile,

                    ApplicationUserId =
                        studentUser.Id,

                    ApplicationUser =
                        studentUser
                };

            _context.StudentProfiles.Add(
                studentProfile);

            await _context.SaveChangesAsync(
                cancellationToken);

            return studentProfile;
        }

        private async Task<int>
            CreateStudentEnrollmentsAsync(
                EnrollmentApplication application,
                StudentProfile studentProfile,
                CancellationToken cancellationToken)
        {
            int createdCount = 0;

            foreach (var selected in
                     application.SelectedSubjects)
            {
                bool enrollmentExists =
                    await _context.StudentEnrollments
                        .AnyAsync(
                            enrollment =>
                                enrollment.StudentProfileId ==
                                studentProfile.StudentProfileId &&
                                enrollment.ClassSessionId ==
                                selected.ClassSessionId &&
                                (enrollment.Status ==
                                    StudentEnrollmentStatus.Active ||
                                 enrollment.Status ==
                                    StudentEnrollmentStatus
                                        .PendingDrop),
                            cancellationToken);

                if (enrollmentExists)
                {
                    continue;
                }

                _context.StudentEnrollments.Add(
                    new StudentEnrollment
                    {
                        StudentProfileId =
                            studentProfile.StudentProfileId,

                        ClassSessionId =
                            selected.ClassSessionId,

                        EnrollmentApplicationId =
                            application.EnrollmentApplicationId,

                        StartDate =
                            application.RequestedStartDate.Date,

                        EnrolledAt =
                            DateTime.UtcNow,

                        HardcopyMaterialsRequested =
                            application
                                .HardcopyMaterialsRequested,

                        MonthlyFeeAtEnrollment =
                            selected.MonthlyFeeAtApplication,

                        Status =
                            StudentEnrollmentStatus.Active
                    });

                createdCount++;
            }

            await _context.SaveChangesAsync(
                cancellationToken);

            return createdCount;
        }

        private static void ValidateStudentInformation(
            EnrollmentApplication application)
        {

            if (string.IsNullOrWhiteSpace(
                    application.StudentIdentificationNumber))
            {
                throw new InvalidOperationException(
                    "Student identification number is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    application.StudentGender))
            {
                throw new InvalidOperationException(
                    "Student gender is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    application.StudentSchool))
            {
                throw new InvalidOperationException(
                    "Student school is required.");
            }
        }
        private static void EnsureIdentitySucceeded(
            IdentityResult result,
            string message)
        {
            if (result.Succeeded)
            {
                return;
            }

            string errors =
                string.Join(
                    ", ",
                    result.Errors.Select(error =>
                        error.Description));

            throw new InvalidOperationException(
                $"{message} {errors}");
        }
    }

    public class AccountProvisioningResult
    {
        public int ParentProfileId { get; set; }

        public string ParentUserId { get; set; }
            = string.Empty;

        public string ParentUserName { get; set; }
            = string.Empty;

        public int StudentProfileId { get; set; }

        public string StudentUserId { get; set; }
            = string.Empty;

        public string StudentUserName { get; set; }
            = string.Empty;

        public int CreatedEnrollmentCount { get; set; }
    }
}
