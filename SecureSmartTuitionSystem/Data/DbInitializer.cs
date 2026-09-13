using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public static class DbInitializer
    {
        /*
         * =====================================================
         * DEMO ACCOUNT INFORMATION
         * =====================================================
         */

        private const string InitialPassword =
            "Password123!";

        private const string AdminUserName =
            "AD00001";

        private const string AdminEmail =
            "admin@gmail.com";

        private const string TutorUserName =
            "TU00001";

        private const string TutorEmail =
            "tutor@gmail.com";

        private const string ParentUserName =
            "PA00001";

        private const string ParentEmail =
            "parent@gmail.com";

        private const string StudentUserName =
            "ST00001";

        private const string StudentEmail =
            "student@gmail.com";


        /*
         * =====================================================
         * MAIN SEED METHOD
         * =====================================================
         */

        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider,
            bool seedBasicAccounts = false)
        {
            using var scope =
                serviceProvider.CreateScope();

            var roleManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        RoleManager<IdentityRole>>();

            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<
                        ApplicationDbContext>();

            /*
             * Always create the required roles.
             */
            await CreateRolesAsync(
                roleManager);

            /*
             * This only creates the four demo accounts when
             * seedBasicAccounts is set to true in Program.cs.
             */
            if (!seedBasicAccounts)
            {
                return;
            }

            /*
             * Create one demo account for each role.
             */
            await SeedBasicAccountsAsync(
                userManager);

            /*
             * Create profiles required by the respective
             * role dashboards.
             */
            await SeedDemoAdminProfileAsync(
                context,
                userManager);

            await SeedDemoTutorProfileAsync(
                context,
                userManager);

                await SeedDemoParentProfileAsync(
                    context,
                    userManager);

                await SeedDemoStudentProfileAsync(
                    context,
                    userManager);
            }


        /*
         * =====================================================
         * ROLE CREATION
         * =====================================================
         */

        private static async Task CreateRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames =
            {
                "Admin",
                "Tutor",
                "Parent",
                "Student"
            };

            foreach (string roleName in roleNames)
            {
                bool roleExists =
                    await roleManager.RoleExistsAsync(
                        roleName);

                if (roleExists)
                {
                    continue;
                }

                IdentityResult result =
                    await roleManager.CreateAsync(
                        new IdentityRole(roleName));

                EnsureIdentitySucceeded(
                    result,
                    $"Unable to create the {roleName} role.");
            }
        }


        /*
         * =====================================================
         * BASIC DEMO ACCOUNTS
         * =====================================================
         */

        private static async Task SeedBasicAccountsAsync(
            UserManager<ApplicationUser> userManager)
        {
            await CreateBasicAccountAsync(
                userManager,
                AdminUserName,
                AdminEmail,
                "System Administrator",
                "Admin");

            await CreateBasicAccountAsync(
                userManager,
                TutorUserName,
                TutorEmail,
                "Test Tutor",
                "Tutor");

            await CreateBasicAccountAsync(
                userManager,
                ParentUserName,
                ParentEmail,
                "Test Parent",
                "Parent");

            await CreateBasicAccountAsync(
                userManager,
                StudentUserName,
                StudentEmail,
                "Test Student",
                "Student");
        }


        /*
         * =====================================================
         * CREATE OR UPDATE ONE DEMO ACCOUNT
         * =====================================================
         */

        private static async Task<ApplicationUser>
            CreateBasicAccountAsync(
                UserManager<ApplicationUser> userManager,
                string userName,
                string email,
                string fullName,
                string roleName)
        {
            ApplicationUser? user =
                await userManager.FindByNameAsync(
                    userName);

            if (user == null)
            {
                ApplicationUser? existingEmailUser =
                    await userManager.FindByEmailAsync(
                        email);

                if (existingEmailUser != null)
                {
                    throw new InvalidOperationException(
                        $"Cannot create {userName}. " +
                        $"The email {email} is already used " +
                        "by another account.");
                }

                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    FullName = fullName,
                    IsActive = true,
                    LockoutEnabled = true,
                    EmailConfirmed = false,
                    MustChangePassword = false,
                    TwoFactorEnabled = false
                };

                IdentityResult createResult =
                    await userManager.CreateAsync(
                        user,
                        InitialPassword);

                EnsureIdentitySucceeded(
                    createResult,
                    $"Unable to create demo account {userName}.");
            }
            else
            {
                bool accountChanged = false;

                if (!string.Equals(
                        user.Email,
                        email,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationUser? emailOwner =
                        await userManager.FindByEmailAsync(
                            email);

                    if (emailOwner != null &&
                        emailOwner.Id != user.Id)
                    {
                        throw new InvalidOperationException(
                            $"Cannot update {userName}. " +
                            $"The email {email} is already used " +
                            "by another account.");
                    }

                    user.Email = email;
                    accountChanged = true;
                }

                if (user.FullName != fullName)
                {
                    user.FullName = fullName;
                    accountChanged = true;
                }

                if (!user.IsActive)
                {
                    user.IsActive = true;
                    accountChanged = true;
                }

                if (user.MustChangePassword)
                {
                    user.MustChangePassword = false;
                    accountChanged = true;
                }

                if (accountChanged)
                {
                    IdentityResult updateResult =
                        await userManager.UpdateAsync(
                            user);

                    EnsureIdentitySucceeded(
                        updateResult,
                        $"Unable to update demo account {userName}.");
                }
            }

            bool alreadyInRole =
                await userManager.IsInRoleAsync(
                    user,
                    roleName);

            if (!alreadyInRole)
            {
                IdentityResult roleResult =
                    await userManager.AddToRoleAsync(
                        user,
                        roleName);

                EnsureIdentitySucceeded(
                    roleResult,
                    $"Unable to assign the {roleName} role " +
                    $"to {userName}.");
            }

            return user;
        }


        /*
         * =====================================================
         * DEMO ADMIN PROFILE
         * =====================================================
         */

        private static async Task
            SeedDemoAdminProfileAsync(
                ApplicationDbContext context,
                UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? adminUser =
                await userManager.FindByNameAsync(
                    AdminUserName);

            if (adminUser == null)
            {
                return;
            }

            AdminProfile? adminProfile =
                await context.AdminProfiles
                    .FirstOrDefaultAsync(profile =>
                        profile.ApplicationUserId ==
                            adminUser.Id);

            if (adminProfile == null)
            {
                adminProfile = new AdminProfile
                {
                    ApplicationUserId =
                        adminUser.Id,

                    FullName =
                        adminUser.FullName ??
                        "System Administrator",

                    Position =
                        "Administrator",

                    CreatedAt =
                        DateTime.UtcNow
                };

                context.AdminProfiles.Add(
                    adminProfile);

                await context.SaveChangesAsync();

                return;
            }

            bool hasChanges = false;

            if (adminProfile.FullName !=
                adminUser.FullName)
            {
                adminProfile.FullName =
                    adminUser.FullName ??
                    "System Administrator";

                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(
                    adminProfile.Position))
            {
                adminProfile.Position =
                    "Administrator";

                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }
        }

        /*
         * =====================================================
         * DEMO STUDENT PROFILE
         * =====================================================
         */

        private static async Task SeedDemoStudentProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var studentUser =
                await userManager.FindByNameAsync(
                    "ST00001");

            if (studentUser == null)
            {
                return;
            }

            var existingStudentProfile =
                await context.StudentProfiles
                    .FirstOrDefaultAsync(
                        student =>
                            student.ApplicationUserId ==
                            studentUser.Id);

            if (existingStudentProfile != null)
            {
                return;
            }

            var parentProfile =
                await context.ParentProfiles
                    .FirstOrDefaultAsync();

            if (parentProfile == null)
            {
                return;
            }

            var studentProfile =
                new StudentProfile
                {
                    ApplicationUserId =
                        studentUser.Id,

                    StudentNumber =
                        "ST00001",

                    FullName =
                        studentUser.FullName
                        ?? "Test Student",

                    IdentificationNumber =
                        "090101-10-1234",

                    DateOfBirth =
                        new DateTime(2009, 1, 1),

                    Gender =
                        "Female",

                    School =
                        "Test School",

                    AcademicLevel =
                        "Form 5",

                    IsActive =
                        true,

                    CreatedAt =
                        DateTime.UtcNow,

                    ParentProfileId =
                        parentProfile.ParentProfileId
                };

            context.StudentProfiles.Add(
                studentProfile);

            await context.SaveChangesAsync();
        }


        /*
         * =====================================================
         * DEMO TUTOR PROFILE
         * =====================================================
         */
        private static async Task CreateRolesAsync(
            RoleManager<IdentityRole> roleManager)

        private static async Task
            SeedDemoTutorProfileAsync(
                ApplicationDbContext context,
                UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? tutorUser =
                await userManager.FindByNameAsync(
                    TutorUserName);

            if (tutorUser == null)
            {
                return;
            }

            TutorProfile? tutorProfile =
                await context.TutorProfiles
                    .FirstOrDefaultAsync(profile =>
                        profile.ApplicationUserId ==
                            tutorUser.Id);

            if (tutorProfile == null)
            {
                tutorProfile = new TutorProfile
                {
                    ApplicationUserId =
                        tutorUser.Id,

                    FullName =
                        tutorUser.FullName ??
                        "Test Tutor",

                    Qualification =
                        "Bachelor's Degree",

                    TeachingExperience =
                        "3 years teaching experience",

                    AreaOfExpertise =
                        "Mathematics",

                    IsActive =
                        true,

                    CreatedAt =
                        DateTime.UtcNow
                };

                context.TutorProfiles.Add(
                    tutorProfile);

                await context.SaveChangesAsync();

                return;
            }

            bool hasChanges = false;

            if (tutorProfile.FullName !=
                tutorUser.FullName)
            {
                tutorProfile.FullName =
                    tutorUser.FullName ??
                    "Test Tutor";

                hasChanges = true;
            }

            if (!tutorProfile.IsActive)
            {
                tutorProfile.IsActive = true;
                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }
        }


        /*
         * =====================================================
         * DEMO PARENT PROFILE
         * =====================================================
         */

        private static async Task
            SeedDemoParentProfileAsync(
                ApplicationDbContext context,
                UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? parentUser =
                await userManager.FindByNameAsync(
                    ParentUserName);

            if (parentUser == null)
            {
                return;
            }

            ParentProfile? parentProfile =
                await context.ParentProfiles
                    .FirstOrDefaultAsync(profile =>
                        profile.ApplicationUserId ==
                            parentUser.Id);

            if (parentProfile == null)
            {
                parentProfile = new ParentProfile
                {
                    ApplicationUserId =
                        parentUser.Id,

                    FullName =
                        parentUser.FullName ??
                        "Test Parent",

                    IdentificationNumber =
                        "000000-00-1234",

                    IdentificationLast4 =
                        "1234",

                    CreatedAt =
                        DateTime.UtcNow
                };

                context.ParentProfiles.Add(
                    parentProfile);

                await context.SaveChangesAsync();

                return;
            }

            bool hasChanges = false;

            if (parentProfile.FullName !=
                parentUser.FullName)
            {
                parentProfile.FullName =
                    parentUser.FullName ??
                    "Test Parent";

                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(
                    parentProfile.IdentificationNumber))
            {
                parentProfile.IdentificationNumber =
                    "000000-00-1234";

                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(
                    parentProfile.IdentificationLast4))
            {
                parentProfile.IdentificationLast4 =
                    "1234";

                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }
        }


        /*
         * =====================================================
         * IDENTITY RESULT VALIDATION
         * =====================================================
         */

        private static void EnsureIdentitySucceeded(
            IdentityResult result,
            string errorMessage)
        {
            if (result.Succeeded)
            {
                return;
            }

            /*
             * =================================================
             * TEST APPLICATION 1 - PENDING REVIEW
             * =================================================
             */

            const string pendingApplicationNumber =
                "EA00001";

            bool pendingApplicationExists =
                await context.EnrollmentApplications
                    .AnyAsync(
                        application =>
                            application
                                .ApplicationNumber ==
                            pendingApplicationNumber);

            if (!pendingApplicationExists)
            {
                var firstClass =
                    availableClasses[0];

                var pendingApplication =
                    new EnrollmentApplication
                    {
                        ApplicationNumber =
                            pendingApplicationNumber,

                        GuardianName =
                            "Sample Guardian",

                        GuardianEmail =
                            "guardian.sample@gmail.com",

                        GuardianPhoneNumber =
                            "012-3456789",

                        GuardianIdentificationLast4 =
                            "5678",

                        StudentName =
                            "Sample Student",

                        StudentDateOfBirth =
                            new DateTime(
                                2013,
                                5,
                                15),

                        AcademicLevel =
                            firstClass.Subject
                                .AcademicLevel,

                        RequestedStartDate =
                            DateTime.Today.AddDays(14),

                        HardcopyMaterialsRequested =
                            false,

                        FinalTwoWeeksConfirmed =
                            false,

                        PotentialDuplicateStudent =
                            false,

                        PotentialDuplicateGuardian =
                            false,

                        Status =
                            EnrollmentStatus.PendingReview,

                        ApplicantNotes =
                            "Test enrollment application " +
                            "for the Admin review workflow.",

                        SubmittedAt =
                            DateTime.UtcNow.AddDays(-2)
                    };

                pendingApplication
                    .SelectedSubjects.Add(
                        new EnrollmentApplicationSubject
                        {
                            ClassSessionId =
                                firstClass
                                    .ClassSessionId,

                            MonthlyFeeAtApplication =
                                firstClass.Subject
                                    .MonthlyFee
                        });

                pendingApplication
                    .StatusHistories.Add(
                        new EnrollmentStatusHistory
                        {
                            PreviousStatus =
                                null,

                            NewStatus =
                                EnrollmentStatus
                                    .PendingReview,

                            ChangedAt =
                                pendingApplication
                                    .SubmittedAt,

                            Remarks =
                                "Enrollment application " +
                                "submitted."
                        });

                context.EnrollmentApplications.Add(
                    pendingApplication);
            }
                context.EnrollmentApplications.Add(
            /*
             * =================================================
             * TEST APPLICATION 2 - DUPLICATE REVIEW
             * =================================================
             */

            const string duplicateApplicationNumber =
                "EA00002";

            bool duplicateApplicationExists =
                await context.EnrollmentApplications
                    .AnyAsync(
                        application =>
                            application
                                .ApplicationNumber ==
                            duplicateApplicationNumber);

            if (!duplicateApplicationExists)
            {
                var selectedClass =
                    availableClasses.Count > 1
                        ? availableClasses[1]
                        : availableClasses[0];

                var duplicateApplication =
                    new EnrollmentApplication
                    {
                        ApplicationNumber =
                            duplicateApplicationNumber,

                        GuardianName =
                            "Existing Guardian Test",

                        GuardianEmail =
                            "existing.guardian@gmail.com",

                        GuardianPhoneNumber =
                            "013-8765432",

                        GuardianIdentificationLast4 =
                            "4321",

                        StudentName =
                            "Existing Student Test",

                        StudentDateOfBirth =
                            new DateTime(
                                2012,
                                8,
                                20),

                        AcademicLevel =
                            selectedClass.Subject
                                .AcademicLevel,

                        RequestedStartDate =
                            DateTime.Today.AddDays(21),

                        HardcopyMaterialsRequested =
                            true,

                        FinalTwoWeeksConfirmed =
                            false,

                        PotentialDuplicateStudent =
                            true,

                        PotentialDuplicateGuardian =
                            true,

                        Status =
                            EnrollmentStatus
                                .DuplicateReview,

                        ApplicantNotes =
                            "Test application containing " +
                            "possible duplicate student and " +
                            "guardian records.",

                        SubmittedAt =
                            DateTime.UtcNow.AddDays(-1)
                    };

                duplicateApplication
                    .SelectedSubjects.Add(
                        new EnrollmentApplicationSubject
                        {
                            ClassSessionId =
                                selectedClass
                                    .ClassSessionId,

                            MonthlyFeeAtApplication =
                                selectedClass.Subject
                                    .MonthlyFee
                        });

                duplicateApplication
                    .StatusHistories.Add(
                        new EnrollmentStatusHistory
                        {
                            PreviousStatus =
                                null,

                            NewStatus =
                                EnrollmentStatus
                                    .DuplicateReview,

                            ChangedAt =
                                duplicateApplication
                                    .SubmittedAt,

                            Remarks =
                                "Potential duplicate records " +
                                "detected during application " +
                                "submission."
                        });

                context.EnrollmentApplications.Add(
                    duplicateApplication);
            }
                                "detected during application " +
                                "submission."
                        });

                context.EnrollmentApplications.Add(
                    duplicateApplication);
            }
                                "detected during application " +
                                "submission."
                        });

                context.EnrollmentApplications.Add(
                    duplicateApplication);
            }

            throw new InvalidOperationException(
                $"{errorMessage} {identityErrors}");
        }
    }
}