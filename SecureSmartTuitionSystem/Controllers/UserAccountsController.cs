
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.ViewModels.Admin;
using System.Data;
using System.Text;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UserAccountsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IAdminAccountSetupEmailService adminEmailService,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<UserAccountsController> logger) : Controller
{
    private const int PageSize = 25;

    private IQueryable<string> AccountIdsInRole(string roleName) =>
        from assignment in db.UserRoles
        join role in db.Roles on assignment.RoleId equals role.Id
        where role.Name == roleName
        select assignment.UserId;


    private IQueryable<AccountRow> Accounts()
    {
        var admins = AccountIdsInRole("Admin");
        var tutors = AccountIdsInRole("Tutor");
        var parents = AccountIdsInRole("Parent");
        var students = AccountIdsInRole("Student");

        return from account in db.Users.AsNoTracking()
               where admins.Contains(account.Id)
                  || tutors.Contains(account.Id)
                  || parents.Contains(account.Id)
                  || students.Contains(account.Id)
               select new AccountRow
               {
                   Id = account.Id,
                   UserId = account.UserName ?? "",
                   Name = account.FullName,
                   Email = account.Email,
                   Role = admins.Contains(account.Id)
                       ? "Admin"
                       : tutors.Contains(account.Id)
                           ? "Tutor"
                           : parents.Contains(account.Id)
                               ? "Parent"
                               : "Student",
                   Active = account.IsActive,
                   SetupPending = account.MustChangePassword,
                   Version = account.ConcurrencyStamp
               };
    }

    // Verify that the current Admin is still permitted
    // to perform account management actions.
    private async Task<bool> CurrentAdminIsUsableAsync()
    {
        string? callerId = users.GetUserId(User);

        if (string.IsNullOrWhiteSpace(callerId))
            return false;

        var caller = await users.FindByIdAsync(callerId);

        return caller != null
            && caller.IsActive
            && !caller.MustChangePassword
            && await users.IsInRoleAsync(caller, "Admin");
    }

    // SQL Server transaction-level lock.
    // Used to coordinate Admin provisioning and status changes.
    private async Task AcquireAdminLockAsync()
    {
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;

            EXEC @result = sys.sp_getapplock
                @Resource = N'EduSmart.AdminProvisioning',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;

            IF @result < 0
                THROW 51003, 'Admin operation is busy.', 1;
            """);
    }

    // Generate and send the Admin account setup email.
    private async Task<bool> TrySendAdminSetupEmailAsync(
        ApplicationUser admin)
    {
        try
        {
            if (!admin.IsActive
                || !admin.MustChangePassword
                || string.IsNullOrWhiteSpace(admin.Email)
                || !await users.IsInRoleAsync(admin, "Admin"))
            {
                return false;
            }

            string? configuredUrl =
                configuration["App:PublicBaseUrl"];

            if (string.IsNullOrWhiteSpace(configuredUrl)
                && environment.IsDevelopment())
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
                || (!environment.IsDevelopment()
                    && baseUri.IsLoopback))
            {
                throw new InvalidOperationException(
                    "App:PublicBaseUrl must be a public HTTPS origin.");
            }

            string token =
                await users.GeneratePasswordResetTokenAsync(admin);

            string setupUrl = QueryHelpers.AddQueryString(
                baseUri.GetLeftPart(UriPartial.Authority)
                    + "/Identity/Account/SetupPassword",
                new Dictionary<string, string?>
                {
                    ["userId"] = admin.Id,
                    ["token"] = WebEncoders.Base64UrlEncode(
                        Encoding.UTF8.GetBytes(token))
                });

            await adminEmailService.SendAsync(
                recipientEmail: admin.Email,
                adminName: admin.FullName ?? "",
                adminUserId: admin.UserName ?? "",
                setupUrl: setupUrl);

            return true;
        }
        catch (Exception ex)
        {
            // Do not log tokens, setup URLs or credentials.
            logger.LogWarning(
                "Admin setup email failed for {UserId}. Error type: {ErrorType}.",
                admin.Id,
                ex.GetType().Name);

            return false;
        }
    }

    // GET: UserAccounts/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await CurrentAdminIsUsableAsync())
            return Forbid();

        return View(new CreateAdminViewModel());
    }

    // POST: UserAccounts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateAdminViewModel model)
    {
        if (!await CurrentAdminIsUsableAsync())
            return Forbid();

        model.FullName = model.FullName?.Trim() ?? "";
        model.Email = model.Email?.Trim() ?? "";
        model.PhoneNumber = model.PhoneNumber?.Trim();
        model.Position = model.Position?.Trim();

        // Revalidate the normalized values.
        ModelState.Clear();

        if (!TryValidateModel(model))
            return View(model);

        if (await users.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered.");

            return View(model);
        }

        ApplicationUser? createdAdmin = null;

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            await AcquireAdminLockAsync();

            // Recheck the caller inside the transaction.
            if (!await CurrentAdminIsUsableAsync())
                return Forbid();

            // Recheck email while holding the lock.
            if (await users.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered.");

                return View(model);
            }

            var existingIds = await db.Users
                .AsNoTracking()
                .Where(u =>
                    u.UserName != null &&
                    u.UserName.StartsWith("AD"))
                .Select(u => u.UserName!)
                .ToListAsync();

            int maxNumber = existingIds
                .Where(id =>
                    id.Length == 7 &&
                    id.StartsWith("AD") &&
                    int.TryParse(id.Substring(2), out _))
                .Select(id => int.Parse(id.Substring(2)))
                .DefaultIfEmpty(0)
                .Max();

            if (maxNumber >= 99999)
            {
                ModelState.AddModelError(
                    "",
                    "The maximum number of Admin accounts has been reached.");

                return View(model);
            }

            string newAdminId = $"AD{maxNumber + 1:D5}";

            var admin = new ApplicationUser
            {
                UserName = newAdminId,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                IsActive = true,
                MustChangePassword = true,
                EmailConfirmed = false,
                LockoutEnabled = true,
                TwoFactorEnabled = false
            };

            var result = await users.CreateAsync(admin);

            if (result.Succeeded)
            {
                result = await users.AddToRoleAsync(
                    admin, "Admin");
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        "", error.Description);
                }

                return View(model);
            }

            db.AdminProfiles.Add(new AdminProfile
            {
                ApplicationUserId = admin.Id,
                FullName = model.FullName,
                Position = string.IsNullOrWhiteSpace(model.Position)
                    ? "Administrator"
                    : model.Position,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            createdAdmin = admin;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Admin account creation failed.");

            ModelState.AddModelError(
                "",
                "Unable to complete Admin account creation. " +
                "Please check User Accounts before retrying.");

            return View(model);
        }

        if (createdAdmin == null)
        {
            ModelState.AddModelError(
                "",
                "The Admin account could not be created.");

            return View(model);
        }

        // Send email only after the database commit.
        bool emailSent =
            await TrySendAdminSetupEmailAsync(createdAdmin);

        TempData[emailSent
            ? "AccountSuccess"
            : "AccountError"] =
            emailSent
                ? $"Admin {createdAdmin.UserName} was created successfully. " +
                  "The setup email was sent."
                : $"Admin {createdAdmin.UserName} was created, " +
                  "but the setup email could not be sent. " +
                  "Please use Resend Setup Email. " +
                  "Do not create another account.";

        return RedirectToAction(nameof(Index));
    }

    // POST: UserAccounts/ResendAdminSetup
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendAdminSetup(
        string userId)
    {
        if (!await CurrentAdminIsUsableAsync())
            return Forbid();

        if (string.IsNullOrWhiteSpace(userId)
            || userId.Length > 450)
        {
            return BadRequest();
        }

        var admin = await users.FindByIdAsync(userId);

        if (admin == null
            || !await users.IsInRoleAsync(admin, "Admin"))
        {
            return NotFound();
        }

        if (!admin.IsActive || !admin.MustChangePassword)
        {
            TempData["AccountError"] =
                "Setup email is only available for active Admin " +
                "accounts that have not completed setup.";

            return RedirectToAction(nameof(Index));
        }

        bool sent =
            await TrySendAdminSetupEmailAsync(admin);

        TempData[sent
            ? "AccountSuccess"
            : "AccountError"] =
            sent
                ? $"Setup email sent to {admin.Email}."
                : "The setup email could not be sent. " +
                  "Please check the email configuration.";

        return RedirectToAction(nameof(Index));
    }

    // GET: UserAccounts/Index
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? role,
        int page = 1)
    {
        if (!await CurrentAdminIsUsableAsync())
            return Forbid();

        search = search?.Trim();

        var accounts = Accounts();

        if (!string.IsNullOrEmpty(search))
        {
            accounts = accounts.Where(a =>
                a.UserId.Contains(search) ||
                (a.Name != null &&
                 a.Name.Contains(search)) ||
                (a.Email != null &&
                 a.Email.Contains(search)));
        }

        if (role is "Admin" or "Tutor" or "Student" or "Parent")
        {
            accounts = accounts.Where(a => a.Role == role);
        }

        var total = await accounts.CountAsync();

        var pageCount = Math.Max(
            1,
            (int)Math.Ceiling(total / (double)PageSize));

        page = Math.Clamp(page, 1, pageCount);

        var rows = await accounts
            .OrderBy(a => a.UserId)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return View(new AccountList(
            rows,
            search,
            role,
            page,
            pageCount,
            total,
            users.GetUserId(User)));
    }

    // GET: UserAccounts/Status
    [HttpGet]
    public async Task<IActionResult> Status(string id)
    {
        if (!await CurrentAdminIsUsableAsync())
            return Forbid();

        var account = await Accounts()
            .SingleOrDefaultAsync(a => a.Id == id);

        if (account == null
            || account.Id == users.GetUserId(User)
            || account.Role == "Tutor")
        {
            return NotFound();
        }

        return View(account);
    }

    // POST: UserAccounts/Status
    [HttpPost]
    [ActionName(nameof(Status))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        string id,
        bool? active,
        string? version)
    {
        string? callerId = users.GetUserId(User);

        if (callerId == null || id == callerId)
            return NotFound();

        if (!active.HasValue
            || string.IsNullOrWhiteSpace(version))
        {
            return BadRequest();
        }

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            // Serialize Admin status operations.
            await AcquireAdminLockAsync();

            if (!await CurrentAdminIsUsableAsync())
                return Forbid();

            var account = await users.FindByIdAsync(id);

            if (account == null)
                return NotFound();

            var roles = await users.GetRolesAsync(account);

            if (roles.Contains("Tutor") ||
                !(roles.Contains("Admin") ||
                  roles.Contains("Student") ||
                  roles.Contains("Parent")))
            {
                return NotFound();
            }

            if (account.ConcurrencyStamp != version)
            {
                TempData["AccountError"] =
                    "The account was changed by another request. " +
                    "Please refresh and try again.";

                return RedirectToAction(nameof(Index));
            }

            // Prevent disabling the last usable Admin.
            if (!active.Value
                && account.IsActive
                && !account.MustChangePassword
                && roles.Contains("Admin"))
            {
                var adminIds = AccountIdsInRole("Admin");

                int usableAdminCount = await db.Users
                    .AsNoTracking()
                    .CountAsync(u =>
                        adminIds.Contains(u.Id) &&
                        u.IsActive &&
                        !u.MustChangePassword);

                if (usableAdminCount <= 1)
                {
                    TempData["AccountError"] =
                        "Cannot deactivate the last usable " +
                        "Administrator account.";

                    return RedirectToAction(nameof(Index));
                }
            }

            account.IsActive = active.Value;

            // Updating the security stamp invalidates
            // existing authentication sessions.
            var result =
                await users.UpdateSecurityStampAsync(account);

            if (result.Succeeded)
            {
                await transaction.CommitAsync();

                TempData["AccountSuccess"] =
                    active.Value
                        ? "Account reactivated."
                        : "Account deactivated.";

                return RedirectToAction(nameof(Index));
            }
        }
        catch (Exception ex) when (
            ex is DbUpdateException ||
            ex is SqlException ||
            ex is DbUpdateConcurrencyException)
        {
            logger.LogWarning(
                "Account status update failed. Error type: {ErrorType}.",
                ex.GetType().Name);
        }

        TempData["AccountError"] =
            "The account could not be updated. " +
            "Check its current status and try again.";

        return RedirectToAction(nameof(Index));
    }

    public sealed class AccountRow
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? Name { get; init; }
        public string? Email { get; init; }
        public required string Role { get; init; }
        public bool Active { get; init; }
        public bool SetupPending { get; init; }
        public string? Version { get; init; }
    }

    public sealed record AccountList(
        IReadOnlyList<AccountRow> Rows,
        string? Search,
        string? Role,
        int Page,
        int PageCount,
        int Total,
        string? CurrentAccountId);
}
