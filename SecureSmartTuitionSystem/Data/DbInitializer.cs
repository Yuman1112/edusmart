using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;

namespace SecureSmartTuitionSystem.Data;

public static class DbInitializer
{
    private const string AdminName = "AD00001";
    private const string AdminEmail = "edusmart2627@gmail.com";

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (string name in new[] { "Admin", "Tutor", "Parent", "Student" })
            if (!await roles.RoleExistsAsync(name))
                Ensure(await roles.CreateAsync(new IdentityRole(name)));
    }

    // Restore the original four test accounts only when test seeding is enabled.
    // Development defaults to enabled; Azure/Production requires explicit opt-in.
    public static bool InitialAccountsEnabled(IConfiguration config, IHostEnvironment environment) =>
        config.GetValue<bool?>("InitialAccounts:Enabled") ?? environment.IsDevelopment();

    public static async Task SeedInitialAccountsAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
    {
        if (!InitialAccountsEnabled(config, environment)) return;
        await SeedRolesAsync(services);
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("InitialAccounts");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var definitions = new[] {
            (Role: "Admin", Name: "AD00001", Email: "edusmart2627@gmail.com", FullName: "System Administrator"),
            (Role: "Tutor", Name: "TU00001", Email: "tutor@gmail.com", FullName: "Test Tutor"),
            (Role: "Parent", Name: "PA00001", Email: "parent@gmail.com", FullName: "Test Parent"),
            (Role: "Student", Name: "ST00001", Email: "student@gmail.com", FullName: "Test Student")
        };
        ParentProfile? newParent = null;
        StudentProfile? newStudent = null;
        foreach (var definition in definitions)
        {
            var normalizedEmail = users.NormalizeEmail(definition.Email);
            var matches = await users.Users.Where(u => u.NormalizedEmail == normalizedEmail).Take(2).ToListAsync();
            var byName = await users.FindByNameAsync(definition.Name);
            if (matches.Count > 1 || (byName != null && !string.Equals(byName.NormalizedEmail, normalizedEmail, StringComparison.Ordinal)))
            {
                logger.LogWarning("Initial {Role} account skipped: its reserved User ID or email conflicts with an existing account. No account was changed.", definition.Role);
                continue;
            }
            var user = matches.SingleOrDefault();
            bool created = user == null;
            if (created)
            {
                user = new ApplicationUser { UserName = definition.Name, Email = definition.Email, FullName = definition.FullName,
                    IsActive = true, LockoutEnabled = true, EmailConfirmed = false, MustChangePassword = false, TwoFactorEnabled = false };
                Ensure(await users.CreateAsync(user, "Password123!"));
                Ensure(await users.AddToRoleAsync(user, definition.Role));
            }
            else
            {
                var roles = await users.GetRolesAsync(user!);
                if (roles.Count != 1 || roles[0] != definition.Role)
                {
                    logger.LogWarning("Initial {Role} account skipped: existing email has a different role. No role or password was changed.", definition.Role);
                    continue;
                }
                // Never reset passwords, reactivate accounts, clear lockouts, or change MFA/setup flags.
            }
            var account = user!;
            if (definition.Role == "Admin" && !await db.AdminProfiles.AnyAsync(p => p.ApplicationUserId == account.Id))
                db.AdminProfiles.Add(new AdminProfile { ApplicationUserId = account.Id, FullName = account.FullName ?? definition.FullName,
                    Position = "Administrator", PermissionLevel = AdminPermissionLevels.High });
            if (definition.Role == "Tutor" && !await db.TutorProfiles.AnyAsync(p => p.ApplicationUserId == account.Id))
                db.TutorProfiles.Add(new TutorProfile { ApplicationUserId = account.Id, FullName = account.FullName ?? definition.FullName,
                    Qualification = "Bachelor's Degree", TeachingExperience = "3 years teaching experience", AreaOfExpertise = "Mathematics", IsActive = account.IsActive });
            if (definition.Role == "Parent" && !await db.ParentProfiles.AnyAsync(p => p.ApplicationUserId == account.Id))
            {
                var parent = new ParentProfile { ApplicationUserId = account.Id, FullName = account.FullName ?? definition.FullName,
                    IdentificationNumber = created ? "000000-00-1234" : null, IdentificationLast4 = created ? "1234" : null };
                db.ParentProfiles.Add(parent); if (created) newParent = parent;
            }
            if (definition.Role == "Student" && !await db.StudentProfiles.AnyAsync(p => p.ApplicationUserId == account.Id))
            {
                var number = account.UserName ?? definition.Name;
                if (await db.StudentProfiles.AnyAsync(p => p.StudentNumber == number))
                    throw new InvalidOperationException("Initial student number conflicts with an existing profile. Existing records were not reassigned.");
                var student = new StudentProfile { ApplicationUserId = account.Id, StudentNumber = number,
                    FullName = account.FullName ?? definition.FullName, IdentificationNumber = created ? "090101-10-1234" : "",
                    DateOfBirth = new DateTime(2009, 1, 1), Gender = "Female", School = "Test School", AcademicLevel = "FORM 5", IsActive = account.IsActive };
                db.StudentProfiles.Add(student); if (created) newStudent = student;
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Initial {Role} account {Result}; existing credentials are preserved.", definition.Role, created ? "created" : "retained");
        }
        // Only link accounts just created together, never infer family relationships for existing users.
        if (newParent != null && newStudent != null)
        {
            db.ParentStudents.Add(new ParentStudent { ParentProfileId = newParent.ParentProfileId, StudentProfileId = newStudent.StudentProfileId,
                Relationship = "Parent", IsPrimaryGuardian = true, IsActive = true });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }

    public static async Task BootstrapAdminAsync(IServiceProvider services, IConfiguration config)
    {
        // Explicit first-deployment switch; no demo accounts or shared passwords.
        if (!config.GetValue<bool>("BootstrapAdmin:Enabled")) return;
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BootstrapAdmin");
        ApplicationUser admin;
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var admins = await users.GetUsersInRoleAsync("Admin");
            if (admins.Count > 0)
            {
                var pending = admins.SingleOrDefault(user => user.UserName == AdminName
                    && string.Equals(user.Email, AdminEmail, StringComparison.OrdinalIgnoreCase)
                    && user.IsActive && user.MustChangePassword && user.PasswordHash == null);
                if (admins.Count != 1 || pending == null)
                {
                    logger.LogInformation("Bootstrap skipped: an administrator already exists.");
                    return;
                }
                admin = pending;
            }
            else
            {
                if (await db.Users.AnyAsync())
                    throw new InvalidOperationException("First-admin bootstrap requires an empty account database. Existing accounts were not changed.");
                _ = PublicOrigin(config);
                admin = new ApplicationUser
                {
                    UserName = AdminName, Email = AdminEmail, FullName = "EduSmart Administrator",
                    IsActive = true, LockoutEnabled = true, EmailConfirmed = false,
                    MustChangePassword = true, TwoFactorEnabled = true
                };
                Ensure(await users.CreateAsync(admin));
                Ensure(await users.AddToRoleAsync(admin, "Admin"));
                db.AdminProfiles.Add(new AdminProfile
                {
                    ApplicationUserId = admin.Id, FullName = admin.FullName,
                    Position = "Administrator", PermissionLevel = AdminPermissionLevels.High,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        }
        try
        {
            string token = await users.GeneratePasswordResetTokenAsync(admin);
            string url = QueryHelpers.AddQueryString(PublicOrigin(config) + "/Identity/Account/SetupPassword",
                new Dictionary<string, string?>
                {
                    ["userId"] = admin.Id,
                    ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
                });
            await scope.ServiceProvider.GetRequiredService<IAdminAccountSetupEmailService>()
                .SendAsync(AdminEmail, admin.FullName ?? "", AdminName, url);
            logger.LogInformation("First administrator setup email sent. Disable BootstrapAdmin:Enabled after setup.");
        }
        catch (Exception exception)
        {
            // Never log tokens, URLs, credentials or SMTP exception messages.
            logger.LogWarning("First administrator setup email failed ({ErrorType}). Configure email and public URL, then restart with bootstrap enabled to retry. The account remains pending.", exception.GetType().Name);
        }
    }

    private static string PublicOrigin(IConfiguration config)
    {
        if (!Uri.TryCreate(config["App:PublicBaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback
            || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("App:PublicBaseUrl must be a public HTTPS origin.");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Account initialization failed: "
                + string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}
