using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public static class DbInitializer
    {
        private const string AdminEmail = "admin@gmail.com";
        private const string TutorEmail = "tutor@gmail.com";

        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider,
            bool seedBasicAccounts = false)
        {
            using var scope = serviceProvider.CreateScope();

            var roleManager =
                scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            await CreateRolesAsync(roleManager);

            if (seedBasicAccounts)
            {
                await SeedBasicAccountsAsync(userManager);

                await SeedDemoParentProfileAsync(
                    context,
                    userManager);
            }

            await AssignRoleAsync(
                userManager,
                AdminEmail,
                "Admin");

            await AssignRoleAsync(
                userManager,
                TutorEmail,
                "Tutor");

            await UpdateTestUserNamesAsync(userManager);

            await SeedClassDataAsync(context, userManager);
        }

        private static async Task SeedBasicAccountsAsync(
            UserManager<ApplicationUser> userManager)
        {
            const string initialPassword = "Password123!";

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
                await userManager.FindByNameAsync(userName);

            if (existingUser != null)
            {
                // Keep existing passwords and account settings unchanged.
                if (!string.Equals(
                        existingUser.Email,
                        email,
                        StringComparison.OrdinalIgnoreCase) ||
                    !await userManager.IsInRoleAsync(existingUser, roleName))
                {
                    throw new InvalidOperationException(
                        $"Demo account {userName} conflicts with an existing account.");
                }

                return;
            }

            var existingEmail =
                await userManager.FindByEmailAsync(email);

            if (existingEmail != null)
            {
                // Do not automatically promote an account based on its email.
                throw new InvalidOperationException(
                    $"Cannot create {userName}: email {email} is already in use.");
            }

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                IsActive = true,
                LockoutEnabled = true,

                // Email verification and first-login changes are deferred
                // for these local demo accounts.
                EmailConfirmed = false,
                MustChangePassword = false,
                TwoFactorEnabled = false
            };

            var createResult =
                await userManager.CreateAsync(user, initialPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    createResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Unable to create {userName}: {errors}");
            }

            var roleResult =
                await userManager.AddToRoleAsync(user, roleName);

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    roleResult.Errors.Select(error => error.Description));

                // Remove the newly created account if role assignment fails.
                var deleteResult = await userManager.DeleteAsync(user);

                if (!deleteResult.Succeeded)
                {
                    errors += "; Account cleanup failed: " +
                        string.Join(
                            ", ",
                            deleteResult.Errors.Select(error => error.Description));
                }

                throw new InvalidOperationException(
                    $"Unable to assign {roleName} to {userName}: {errors}");
            }
        }

        private static async Task SeedDemoParentProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var parentUser =
                await userManager.FindByNameAsync("PA00001");

            if (parentUser == null)
            {
                return;
            }

            var existingParentProfile =
                await context.ParentProfiles
                    .FirstOrDefaultAsync(
                        parentProfile =>
                            parentProfile.ApplicationUserId ==
                            parentUser.Id);

            // ParentProfile does not exist yet.
            if (existingParentProfile == null)
            {
                var parentProfile =
                    new ParentProfile
                    {
                        ApplicationUserId = parentUser.Id,

                        FullName =
                            parentUser.FullName
                            ?? "Test Parent",

                        IdentificationNumber = "000000-00-1234",
                        IdentificationLast4 = "1234",

                        CreatedAt = DateTime.UtcNow
                    };

                context.ParentProfiles.Add(
                    parentProfile);

                await context.SaveChangesAsync();

                return;
            }

            bool hasChanges = false;

            if (string.IsNullOrWhiteSpace(
                    existingParentProfile.IdentificationNumber))
            {
                existingParentProfile.IdentificationNumber =
                    "000000-00-1234";

                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(
                    existingParentProfile.IdentificationLast4))
            {
                existingParentProfile.IdentificationLast4 =
                    "1234";

                hasChanges = true;
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }
        }

        private static async Task CreateRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
                "Admin",
                "Tutor",
                "Parent",
                "Student"
            };

            foreach (var roleName in roles)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var result = await roleManager.CreateAsync(
                    new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(error =>
                            error.Description));

                    throw new InvalidOperationException(
                        $"Unable to create role {roleName}: {errors}");
                }
            }
        }

        private static async Task AssignRoleAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string roleName)
        {
            var user = await userManager.FindByEmailAsync(email);

            // The account must be registered before
            // the role can be assigned.
            if (user == null)
            {
                return;
            }

            if (await userManager.IsInRoleAsync(user, roleName))
            {
                return;
            }

            var result = await userManager.AddToRoleAsync(
                user,
                roleName);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(error =>
                        error.Description));

                throw new InvalidOperationException(
                    $"Unable to assign {roleName} role " +
                    $"to {email}: {errors}");
            }
        }

        private static async Task UpdateTestUserNamesAsync(
            UserManager<ApplicationUser> userManager)
        {
            var admin =
                await userManager.FindByEmailAsync(AdminEmail);

            if (admin != null &&
                string.IsNullOrWhiteSpace(admin.FullName))
            {
                admin.FullName = "System Administrator";

                var result =
                    await userManager.UpdateAsync(admin);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to update Admin profile.");
                }
            }

            var tutor =
                await userManager.FindByEmailAsync(TutorEmail);

            if (tutor != null &&
                string.IsNullOrWhiteSpace(tutor.FullName))
            {
                tutor.FullName = "Test Tutor";

                var result =
                    await userManager.UpdateAsync(tutor);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to update Tutor profile.");
                }
            }
        }

        private static async Task SeedClassDataAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var mathematics =
                await context.Subjects.FirstOrDefaultAsync(
                    subject =>
                        subject.SubjectName == "Mathematics" &&
                        subject.AcademicLevel == "Form 3");

            if (mathematics == null)
            {
                mathematics = new Subject
                {
                    SubjectName = "Mathematics",
                    AcademicLevel = "Form 3",
                    MonthlyFee = 200.00m,
                    IsActive = true
                };

                context.Subjects.Add(mathematics);
            }

            var science =
                await context.Subjects.FirstOrDefaultAsync(
                    subject =>
                        subject.SubjectName == "Science" &&
                        subject.AcademicLevel == "Form 3");

            if (science == null)
            {
                science = new Subject
                {
                    SubjectName = "Science",
                    AcademicLevel = "Form 3",
                    MonthlyFee = 220.00m,
                    IsActive = true
                };

                context.Subjects.Add(science);
            }

            await context.SaveChangesAsync();

            var tutor =
                await userManager.FindByEmailAsync(TutorEmail);

            // ClassSession requires an existing Tutor account.
            if (tutor == null)
            {
                return;
            }

            bool mathematicsClassExists =
                await context.ClassSessions.AnyAsync(
                    session =>
                        session.ClassCode == "MATH-F3-01");

            if (!mathematicsClassExists)
            {
                context.ClassSessions.Add(
                    new ClassSession
                    {
                        ClassCode = "MATH-F3-01",
                        SubjectId = mathematics.SubjectId,
                        TutorUserId = tutor.Id,
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(18, 0),
                        EndTime = new TimeOnly(20, 0),
                        Venue = "Room A",
                        Capacity = 25,
                        Status = ClassSessionStatus.Active
                    });
            }

            bool scienceClassExists =
                await context.ClassSessions.AnyAsync(
                    session =>
                        session.ClassCode == "SCI-F3-01");

            if (!scienceClassExists)
            {
                context.ClassSessions.Add(
                    new ClassSession
                    {
                        ClassCode = "SCI-F3-01",
                        SubjectId = science.SubjectId,
                        TutorUserId = tutor.Id,
                        DayOfWeek = DayOfWeek.Wednesday,
                        StartTime = new TimeOnly(18, 0),
                        EndTime = new TimeOnly(20, 0),
                        Venue = "Room B",
                        Capacity = 25,
                        Status = ClassSessionStatus.Active
                    });
            }

            await context.SaveChangesAsync();
        }
    }
}