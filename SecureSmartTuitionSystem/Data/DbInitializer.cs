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
                    Position = "Administrator", CreatedAt = DateTime.UtcNow
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
