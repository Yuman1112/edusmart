using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public static class DbInitializer
    {
        private const string AdminEmail =
            "admin@gmail.com";

        private const string TutorEmail =
            "tutor@gmail.com";

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

            await CreateRolesAsync(roleManager);

            /*
             * This only creates the four demo accounts when
             * seedBasicAccounts is set to true in Program.cs.
             */
            if (seedBasicAccounts)
            {
                await SeedBasicAccountsAsync(
                    userManager);

                await SeedDemoAdminProfileAsync(
                    context,
                    userManager);

                await SeedDemoTutorProfileAsync(
                    context,
                    userManager);

                await SeedDemoParentProfileAsync(
                    context,
                    userManager);
            }

            /*
             * Assign roles to accounts that already exist.
             */
            await AssignRoleAsync(
                userManager,
                AdminEmail,
                "Admin");

            await AssignRoleAsync(
                userManager,
                TutorEmail,
                "Tutor");

            await UpdateTestUserNamesAsync(
                userManager);

            /*
             * Class Sessions are now managed by Admin.
             * Do not automatically create legacy demo classes because
             * CS00001 and CS00002 may already exist.
             */

            await SeedTestEnrollmentApplicationsAsync(
                context);
        }

        /*
         * =====================================================
         * BASIC DEMO ACCOUNTS
         * =====================================================
         */

        private static async Task SeedBasicAccountsAsync(
            UserManager<ApplicationUser> userManager)
        {
            const string initialPassword =
                "Password123!";

            await CreateBasicAccountAsync(
                userManager,
                "AD00001",
                AdminEmail,
                "System Administrator",
                "Admin",
                initialPassword);

            await CreateBasicAccountAsync(
                userManager,
                "TU00001",
                TutorEmail,
                "Test Tutor",
                "Tutor",
                initialPassword);

            await CreateBasicAccountAsync(
                userManager,
                "PA00001",
                "parent@gmail.com",
                "Test Parent",
                "Parent",
                initialPassword);

            await CreateBasicAccountAsync(
                userManager,
                "ST00001",
                "student@gmail.com",
                "Test Student",
                "Student",
                initialPassword);
        }

        private static async Task CreateBasicAccountAsync(
            UserManager<ApplicationUser> userManager,
            string userName,
            string email,
            string fullName,
            string roleName,
            string initialPassword)
        {
            var existingUser =
                await userManager.FindByNameAsync(
                    userName);

            if (existingUser != null)
            {
                bool sameEmail =
                    string.Equals(
                        existingUser.Email,
                        email,
                        StringComparison.OrdinalIgnoreCase);

                bool hasExpectedRole =
                    await userManager.IsInRoleAsync(
                        existingUser,
                        roleName);

                if (!sameEmail ||
                    !hasExpectedRole)
                {
                    throw new InvalidOperationException(
                        $"Demo account {userName} conflicts " +
                        "with an existing account.");
                }

                return;
            }

            var existingEmail =
                await userManager.FindByEmailAsync(
                    email);

            if (existingEmail != null)
            {
                throw new InvalidOperationException(
                    $"Cannot create {userName}: email " +
                    $"{email} is already in use.");
            }

            var user =
                new ApplicationUser
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

            var createResult =
                await userManager.CreateAsync(
                    user,
                    initialPassword);

            if (!createResult.Succeeded)
            {
                string errors =
                    string.Join(
                        ", ",
                        createResult.Errors.Select(
                            error =>
                                error.Description));

                throw new InvalidOperationException(
                    $"Unable to create {userName}: " +
                    $"{errors}");
            }

            var roleResult =
                await userManager.AddToRoleAsync(
                    user,
                    roleName);

            if (!roleResult.Succeeded)
            {
                string errors =
                    string.Join(
                        ", ",
                        roleResult.Errors.Select(
                            error =>
                                error.Description));

                var deleteResult =
                    await userManager.DeleteAsync(
                        user);

                if (!deleteResult.Succeeded)
                {
                    errors +=
                        "; Account cleanup failed: " +
                        string.Join(
                            ", ",
                            deleteResult.Errors.Select(
                                error =>
                                    error.Description));
                }

                throw new InvalidOperationException(
                    $"Unable to assign {roleName} to " +
                    $"{userName}: {errors}");
            }
        }

        private static async Task SeedDemoAdminProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var adminUser =
                await userManager.FindByNameAsync("AD00001");

            if (adminUser == null)
            {
                return;
            }

            var existingAdminProfile =
                await context.AdminProfiles
                    .FirstOrDefaultAsync(
                        adminProfile =>
                            adminProfile.ApplicationUserId ==
                            adminUser.Id);

            if (existingAdminProfile != null)
            {
                return;
            }

            var adminProfile = new AdminProfile
            {
                ApplicationUserId = adminUser.Id,

                FullName =
                    adminUser.FullName
                    ?? "System Administrator",

                Position = "Administrator",

                CreatedAt = DateTime.UtcNow
            };

            context.AdminProfiles.Add(adminProfile);

            await context.SaveChangesAsync();
        }

        private static async Task SeedDemoTutorProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var tutorUser =
                await userManager.FindByNameAsync("TU00001");

            if (tutorUser == null)
            {
                return;
            }

            var existingTutorProfile =
                await context.TutorProfiles
                    .FirstOrDefaultAsync(
                        tutorProfile =>
                            tutorProfile.ApplicationUserId ==
                            tutorUser.Id);

            if (existingTutorProfile != null)
            {
                return;
            }

            var tutorProfile = new TutorProfile
            {
                ApplicationUserId = tutorUser.Id,

                FullName =
                    tutorUser.FullName
                    ?? "Test Tutor",

                Qualification = "Bachelor's Degree",

                TeachingExperience =
                    "3 years teaching experience",

                AreaOfExpertise = "Mathematics",

                IsActive = true,

                CreatedAt = DateTime.UtcNow
            };

            context.TutorProfiles.Add(tutorProfile);

            await context.SaveChangesAsync();
        }

        /*
         * =====================================================
         * DEMO PARENT PROFILE
         * =====================================================
         */

        private static async Task SeedDemoParentProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var parentUser =
                await userManager.FindByNameAsync(
                    "PA00001");

            if (parentUser == null)
            {
                return;
            }

            var existingParentProfile =
                await context.ParentProfiles
                    .FirstOrDefaultAsync(
                        parentProfile =>
                            parentProfile
                                .ApplicationUserId ==
                            parentUser.Id);

            if (existingParentProfile == null)
            {
                var parentProfile =
                    new ParentProfile
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

            if (string.IsNullOrWhiteSpace(
                existingParentProfile
                    .IdentificationNumber))
            {
                existingParentProfile
                    .IdentificationNumber =
                    "000000-00-1234";

                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(
                existingParentProfile
                    .IdentificationLast4))
            {
                existingParentProfile
                    .IdentificationLast4 =
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
         * ROLE CREATION
         * =====================================================
         */

        private static async Task CreateRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
                "Admin",
                "Tutor",
                "Parent",
                "Student"
            };

            foreach (string roleName in roles)
            {
                bool roleExists =
                    await roleManager.RoleExistsAsync(
                        roleName);

                if (roleExists)
                {
                    continue;
                }

                var result =
                    await roleManager.CreateAsync(
                        new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    string errors =
                        string.Join(
                            ", ",
                            result.Errors.Select(
                                error =>
                                    error.Description));

                    throw new InvalidOperationException(
                        $"Unable to create role " +
                        $"{roleName}: {errors}");
                }
            }
        }

        /*
         * =====================================================
         * ROLE ASSIGNMENT
         * =====================================================
         */

        private static async Task AssignRoleAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string roleName)
        {
            var user =
                await userManager.FindByEmailAsync(
                    email);

            /*
             * The account must already be registered before
             * the role can be assigned.
             */
            if (user == null)
            {
                return;
            }

            bool alreadyInRole =
                await userManager.IsInRoleAsync(
                    user,
                    roleName);

            if (alreadyInRole)
            {
                return;
            }

            var result =
                await userManager.AddToRoleAsync(
                    user,
                    roleName);

            if (!result.Succeeded)
            {
                string errors =
                    string.Join(
                        ", ",
                        result.Errors.Select(
                            error =>
                                error.Description));

                throw new InvalidOperationException(
                    $"Unable to assign {roleName} role " +
                    $"to {email}: {errors}");
            }
        }

        /*
         * =====================================================
         * UPDATE TEST USER DISPLAY NAMES
         * =====================================================
         */

        private static async Task UpdateTestUserNamesAsync(
            UserManager<ApplicationUser> userManager)
        {
            var admin =
                await userManager.FindByEmailAsync(
                    AdminEmail);

            if (admin != null &&
                string.IsNullOrWhiteSpace(
                    admin.FullName))
            {
                admin.FullName =
                    "System Administrator";

                var result =
                    await userManager.UpdateAsync(
                        admin);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to update Admin profile.");
                }
            }

            var tutor =
                await userManager.FindByEmailAsync(
                    TutorEmail);

            if (tutor != null &&
                string.IsNullOrWhiteSpace(
                    tutor.FullName))
            {
                tutor.FullName =
                    "Test Tutor";

                var result =
                    await userManager.UpdateAsync(
                        tutor);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to update Tutor profile.");
                }
            }
        }

        /*
         * =====================================================
         * SUBJECT AND CLASS SESSION DATA
         * =====================================================
         */

        private static async Task SeedClassDataAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var mathematics =
                await context.Subjects
                    .FirstOrDefaultAsync(
                        subject =>
                            subject.SubjectName ==
                                "Mathematics" &&
                            subject.AcademicLevel ==
                                "Form 3");

            if (mathematics == null)
            {
                mathematics =
                    new Subject
                    {
                        EducationStage =
                            EducationStage.Secondary,

                        AcademicLevel =
                            "Form 3",

                        SubjectName =
                            "Mathematics",

                        MonthlyFee =
                            200.00m,

                        IsActive =
                            true
                    };

                context.Subjects.Add(
                    mathematics);
            }
            else
            {
                mathematics.EducationStage =
                    EducationStage.Secondary;

                mathematics.IsActive =
                    true;
            }

            var science =
                await context.Subjects
                    .FirstOrDefaultAsync(
                        subject =>
                            subject.SubjectName ==
                                "Science" &&
                            subject.AcademicLevel ==
                                "Form 3");

            if (science == null)
            {
                science =
                    new Subject
                    {
                        EducationStage =
                            EducationStage.Secondary,

                        AcademicLevel =
                            "Form 3",

                        SubjectName =
                            "Science",

                        MonthlyFee =
                            220.00m,

                        IsActive =
                            true
                    };

                context.Subjects.Add(
                    science);
            }
            else
            {
                science.EducationStage =
                    EducationStage.Secondary;

                science.IsActive =
                    true;
            }

            await context.SaveChangesAsync();

            var tutor =
                await userManager.FindByEmailAsync(
                    TutorEmail);

            /*
             * ClassSession requires an existing Tutor.
             */
            if (tutor == null)
            {
                return;
            }

            var mathematicsClass =
                await context.ClassSessions
                    .FirstOrDefaultAsync(
                        session =>
                            session.ClassCode ==
                                "CS00001");

            if (mathematicsClass == null)
            {
                mathematicsClass =
                    new ClassSession
                    {
                        ClassCode =
                            "CS00001",

                        ClassName =
                            "FORM 3 - MM - " +
                            "Test Tutor - 06:00 PM",

                        SubjectId =
                            mathematics.SubjectId,

                        TutorUserId =
                            tutor.Id,

                        DayOfWeek =
                            DayOfWeek.Monday,

                        StartTime =
                            new TimeOnly(18, 0),

                        EndTime =
                            new TimeOnly(20, 0),

                        Venue =
                            "https://meet.google.com/" +
                            "math-f3-demo",

                        Capacity =
                            25,

                        Status =
                            ClassSessionStatus.Active
                    };

                context.ClassSessions.Add(
                    mathematicsClass);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(
                    mathematicsClass.Venue) ||
                    !mathematicsClass.Venue.StartsWith(
                        "https://meet.google.com/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    mathematicsClass.Venue =
                        "https://meet.google.com/" +
                        "math-f3-demo";
                }

                if (string.IsNullOrWhiteSpace(
                    mathematicsClass.ClassName))
                {
                    mathematicsClass.ClassName =
                        "FORM 3 - MM - " +
                        "Test Tutor - 06:00 PM";
                }
            }

            var scienceClass =
                await context.ClassSessions
                    .FirstOrDefaultAsync(
                        session =>
                            session.ClassCode ==
                                "CS00002");

            if (scienceClass == null)
            {
                scienceClass =
                    new ClassSession
                    {
                        ClassCode =
                            "CS00002",

                        ClassName =
                            "FORM 3 - SCI - " +
                            "Test Tutor - 06:00 PM",

                        SubjectId =
                            science.SubjectId,

                        TutorUserId =
                            tutor.Id,

                        DayOfWeek =
                            DayOfWeek.Wednesday,

                        StartTime =
                            new TimeOnly(18, 0),

                        EndTime =
                            new TimeOnly(20, 0),

                        Venue =
                            "https://meet.google.com/" +
                            "science-f3-demo",

                        Capacity =
                            25,

                        Status =
                            ClassSessionStatus.Active
                    };

                context.ClassSessions.Add(
                    scienceClass);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(
                    scienceClass.Venue) ||
                    !scienceClass.Venue.StartsWith(
                        "https://meet.google.com/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    scienceClass.Venue =
                        "https://meet.google.com/" +
                        "science-f3-demo";
                }

                if (string.IsNullOrWhiteSpace(
                    scienceClass.ClassName))
                {
                    scienceClass.ClassName =
                        "FORM 3 - SCI - " +
                        "Test Tutor - 06:00 PM";
                }
            }

            await context.SaveChangesAsync();
        }

        /*
         * =====================================================
         * TEST ENROLLMENT APPLICATIONS
         * =====================================================
         */

        private static async Task
            SeedTestEnrollmentApplicationsAsync(
                ApplicationDbContext context)
        {
            /*
             * Find two active classes for testing.
             */
            var availableClasses =
                await context.ClassSessions
                    .AsNoTracking()
                    .Include(session =>
                        session.Subject)
                    .Where(session =>
                        session.Status ==
                        ClassSessionStatus.Active)
                    .OrderBy(session =>
                        session.ClassSessionId)
                    .Take(2)
                    .ToListAsync();

            /*
             * EnrollmentApplicationSubject requires an
             * existing ClassSession.
             */
            if (availableClasses.Count == 0)
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

            await context.SaveChangesAsync();
        }
    }
}