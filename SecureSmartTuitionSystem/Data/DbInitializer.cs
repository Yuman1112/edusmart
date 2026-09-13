using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public static class DbInitializer
    {
        private const string InitialPassword = "Password123!";
        private const string AdminUserName = "AD00001";
        private const string AdminEmail = "admin@gmail.com";
        private const string TutorUserName = "TU00001";
        private const string TutorEmail = "tutor@gmail.com";
        private const string ParentUserName = "PA00001";
        private const string ParentEmail = "parent@gmail.com";
        private const string StudentUserName = "ST00001";
        private const string StudentEmail = "student@gmail.com";

        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider,
            bool seedBasicAccounts = false)
        {
            using IServiceScope scope = serviceProvider.CreateScope();

            RoleManager<IdentityRole> roleManager =
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            UserManager<ApplicationUser> userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationDbContext context =
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await CreateRolesAsync(roleManager);

            if (!seedBasicAccounts)
            {
                return;
            }

            await SeedBasicAccountsAsync(userManager);
            await SeedAdminProfileAsync(context, userManager);
            await SeedTutorProfileAsync(context, userManager);
            await SeedParentProfileAsync(context, userManager);
            await SeedStudentProfileAsync(context, userManager);
        }

        private static async Task CreateRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "Admin", "Tutor", "Parent", "Student" };

            foreach (string roleName in roleNames)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                IdentityResult result = await roleManager.CreateAsync(
                    new IdentityRole(roleName));

                EnsureIdentitySucceeded(
                    result,
                    $"Unable to create the {roleName} role.");
            }
        }

        private static async Task SeedBasicAccountsAsync(
            UserManager<ApplicationUser> userManager)
        {
            await CreateBasicAccountAsync(
                userManager, AdminUserName, AdminEmail,
                "System Administrator", "Admin");
            await CreateBasicAccountAsync(
                userManager, TutorUserName, TutorEmail,
                "Test Tutor", "Tutor");
            await CreateBasicAccountAsync(
                userManager, ParentUserName, ParentEmail,
                "Test Parent", "Parent");
            await CreateBasicAccountAsync(
                userManager, StudentUserName, StudentEmail,
                "Test Student", "Student");
        }

        private static async Task<ApplicationUser> CreateBasicAccountAsync(
            UserManager<ApplicationUser> userManager,
            string userName,
            string email,
            string fullName,
            string roleName)
        {
            ApplicationUser? user = await userManager.FindByNameAsync(userName);

            if (user == null)
            {
                ApplicationUser? emailOwner =
                    await userManager.FindByEmailAsync(email);

                if (emailOwner != null)
                {
                    throw new InvalidOperationException(
                        $"Cannot create {userName}. The email {email} " +
                        "is already used by another account.");
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
                    await userManager.CreateAsync(user, InitialPassword);
                EnsureIdentitySucceeded(
                    createResult,
                    $"Unable to create demo account {userName}.");
            }
            else
            {
                bool changed = false;

                if (!string.Equals(
                        user.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationUser? emailOwner =
                        await userManager.FindByEmailAsync(email);

                    if (emailOwner != null && emailOwner.Id != user.Id)
                    {
                        throw new InvalidOperationException(
                            $"Cannot update {userName}. The email {email} " +
                            "is already used by another account.");
                    }

                    user.Email = email;
                    changed = true;
                }

                if (user.FullName != fullName)
                {
                    user.FullName = fullName;
                    changed = true;
                }

                if (!user.IsActive)
                {
                    user.IsActive = true;
                    changed = true;
                }

                if (user.MustChangePassword)
                {
                    user.MustChangePassword = false;
                    changed = true;
                }

                if (changed)
                {
                    IdentityResult updateResult =
                        await userManager.UpdateAsync(user);
                    EnsureIdentitySucceeded(
                        updateResult,
                        $"Unable to update demo account {userName}.");
                }
            }

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                IdentityResult roleResult =
                    await userManager.AddToRoleAsync(user, roleName);
                EnsureIdentitySucceeded(
                    roleResult,
                    $"Unable to assign the {roleName} role to {userName}.");
            }

            return user;
        }

        private static async Task SeedAdminProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? user =
                await userManager.FindByNameAsync(AdminUserName);
            if (user == null) return;

            AdminProfile? profile = await context.AdminProfiles
                .FirstOrDefaultAsync(x => x.ApplicationUserId == user.Id);

            if (profile == null)
            {
                context.AdminProfiles.Add(new AdminProfile
                {
                    ApplicationUserId = user.Id,
                    FullName = user.FullName ?? "System Administrator",
                    Position = "Administrator",
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                return;
            }

            bool changed = false;
            string fullName = user.FullName ?? "System Administrator";

            if (profile.FullName != fullName)
            {
                profile.FullName = fullName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(profile.Position))
            {
                profile.Position = "Administrator";
                changed = true;
            }

            if (changed) await context.SaveChangesAsync();
        }

        private static async Task SeedTutorProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? user =
                await userManager.FindByNameAsync(TutorUserName);
            if (user == null) return;

            TutorProfile? profile = await context.TutorProfiles
                .FirstOrDefaultAsync(x => x.ApplicationUserId == user.Id);

            if (profile == null)
            {
                context.TutorProfiles.Add(new TutorProfile
                {
                    ApplicationUserId = user.Id,
                    FullName = user.FullName ?? "Test Tutor",
                    Qualification = "Bachelor's Degree",
                    TeachingExperience = "3 years teaching experience",
                    AreaOfExpertise = "Mathematics",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                return;
            }

            bool changed = false;
            string fullName = user.FullName ?? "Test Tutor";

            if (profile.FullName != fullName)
            {
                profile.FullName = fullName;
                changed = true;
            }

            if (!profile.IsActive)
            {
                profile.IsActive = true;
                changed = true;
            }

            if (changed) await context.SaveChangesAsync();
        }

        private static async Task SeedParentProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? user =
                await userManager.FindByNameAsync(ParentUserName);
            if (user == null) return;

            ParentProfile? profile = await context.ParentProfiles
                .FirstOrDefaultAsync(x => x.ApplicationUserId == user.Id);

            if (profile == null)
            {
                context.ParentProfiles.Add(new ParentProfile
                {
                    ApplicationUserId = user.Id,
                    FullName = user.FullName ?? "Test Parent",
                    IdentificationNumber = "000000-00-1234",
                    IdentificationLast4 = "1234",
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                return;
            }

            string fullName = user.FullName ?? "Test Parent";
            if (profile.FullName != fullName)
            {
                profile.FullName = fullName;
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedStudentProfileAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            ApplicationUser? user =
                await userManager.FindByNameAsync(StudentUserName);
            if (user == null) return;

            bool exists = await context.StudentProfiles
                .AnyAsync(x => x.ApplicationUserId == user.Id);
            if (exists) return;

            ApplicationUser? parentUser =
                await userManager.FindByNameAsync(ParentUserName);
            if (parentUser == null) return;

            ParentProfile? parent = await context.ParentProfiles
                .FirstOrDefaultAsync(
                    x => x.ApplicationUserId == parentUser.Id);
            if (parent == null) return;

            context.StudentProfiles.Add(new StudentProfile
            {
                ApplicationUserId = user.Id,
                StudentNumber = StudentUserName,
                FullName = user.FullName ?? "Test Student",
                IdentificationNumber = "090101-10-1234",
                DateOfBirth = new DateTime(2009, 1, 1),
                Gender = "Female",
                School = "Test School",
                AcademicLevel = "Form 5",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ParentProfileId = parent.ParentProfileId
            });

            await context.SaveChangesAsync();
        }

        private static void EnsureIdentitySucceeded(
            IdentityResult result,
            string errorMessage)
        {
            if (result.Succeeded) return;

            string errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));

            throw new InvalidOperationException($"{errorMessage} {errors}");
        }
    }
}
