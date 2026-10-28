using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TutorManagementController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    AccountProvisioningService provisioning,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ITutorAccountSetupEmailService tutorEmailService,
    TutorEmailChangeService tutorEmailChanges,
    AuditLogIntegrity auditIntegrity,
    ILogger<TutorManagementController> logger) : Controller
{
    // ============================================
    // ADMIN - DEACTIVATE / REACTIVATE TUTOR
    // ============================================
    [HttpGet]
    public async Task<IActionResult> ChangeStatus(
        string userId,
        bool activate)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var profile = await db.TutorProfiles
            .AsNoTracking()
            .Include(p => p.ApplicationUser)
            .FirstOrDefaultAsync(p =>
                p.ApplicationUserId == userId);

        if (profile == null)
            return NotFound();

        if (!await users.IsInRoleAsync(
                profile.ApplicationUser, "Tutor")
            || await users.IsInRoleAsync(
                profile.ApplicationUser, "Admin"))
        {
            return Forbid();
        }

        var model = new ChangeTutorStatusViewModel
        {
            UserId = userId,
            Activate = activate,
            TutorId = profile.ApplicationUser.UserName ?? "",
            FullName = profile.FullName,
            SetupPending = profile.ApplicationUser.MustChangePassword
        };

        if (!activate)
        {
            model.BlockingAssignments =
                await new TutorAccountStatusService(db, users, auditIntegrity)
                    .GetBlockingAssignmentsAsync(userId);
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(ChangeTutorStatusViewModel model)
    {
        if (!model.Activate.HasValue || string.IsNullOrWhiteSpace(model.UserId)
            || model.UserId.Length > 450) return BadRequest();

        model.Reason = model.Reason?.Trim() ?? string.Empty;
        if (!ModelState.IsValid)
        {
            var profile = await db.TutorProfiles.AsNoTracking().Include(p => p.ApplicationUser)
                .FirstOrDefaultAsync(p => p.ApplicationUserId == model.UserId);
            if (profile == null) return NotFound();
            if (!await users.IsInRoleAsync(profile.ApplicationUser, "Tutor")
                || await users.IsInRoleAsync(profile.ApplicationUser, "Admin")) return Forbid();
            model.TutorId = profile.ApplicationUser.UserName ?? "";
            model.FullName = profile.FullName;
            model.SetupPending = profile.ApplicationUser.MustChangePassword;
            if (model.Activate == false)
                model.BlockingAssignments = await new TutorAccountStatusService(db, users, auditIntegrity)
                    .GetBlockingAssignmentsAsync(model.UserId);
            return View(model);
        }

        string? actorUserId = users.GetUserId(User);
        if (string.IsNullOrWhiteSpace(actorUserId)) return Forbid();
        try
        {
            var result = await new TutorAccountStatusService(db, users, auditIntegrity)
                .ChangeAsync(model.UserId, model.Activate.Value, actorUserId, model.Reason);
            TempData[result.Success ? "TutorSuccess" : "TutorWarning"] = result.Message;
        }
        catch (DbUpdateException)
        {
            TempData["TutorWarning"] = "The account status could not be saved. Reload the page and try again.";
        }
        return RedirectToAction(nameof(Details), new { userId = model.UserId });
    }

    // ============================================
    // ADMIN - EXPORT TUTOR RECORDS
    // ============================================
    [HttpGet]
    public async Task<IActionResult> Export(
        [FromQuery] TutorExportFilterViewModel filter, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var records = await new TutorExportService(db).GetRecordsAsync(filter, cancellationToken);
        return View(TutorExportService.Preview(filter, records));
    }

    [HttpPost]
    public async Task<IActionResult> DownloadTutorRecords(
        TutorExportFilterViewModel filter, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var records = await new TutorExportService(db).GetRecordsAsync(filter, cancellationToken);
        if (records.Count == 0)
            return View("Export", TutorExportService.Preview(filter, records));

        bool excel = filter.Format == "xlsx";
        byte[] content = excel ? TutorExportService.Excel(records) : TutorExportService.Csv(records);
        var audit = new AuditLogEntry
        {
            ActorUserId = users.GetUserId(User),
            ActorName = User.Identity?.Name ?? "Administrator",
            EventName = "TutorRecordsExported",
            Severity = SecuritySeverity.Information,
            EntityName = "TutorProfile",
            Summary = $"Generated export of {records.Count} tutor records as {(excel ? "XLSX" : "CSV")}."
        };
        auditIntegrity.Seal(audit);
        db.AuditLogEntries.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        string stamp = DateTime.UtcNow.AddHours(8).ToString("yyyyMMdd_HHmmss");
        return File(content,
            excel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv; charset=utf-8",
            $"EduSmart_Tutors_{stamp}.{(excel ? "xlsx" : "csv")}");
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rows = await db.TutorProfiles.AsNoTracking()
            .OrderBy(x => x.ApplicationUser.UserName)
            .Select(x => new TutorListItem
            {
                UserId = x.ApplicationUserId,
                UserName = x.ApplicationUser.UserName ?? "",
                FullName = x.FullName,
                Email = x.ApplicationUser.Email ?? "",
                AreaOfExpertise = x.AreaOfExpertise ?? "",
                IsActive = x.IsActive && x.ApplicationUser.IsActive,
                SetupPending = x.ApplicationUser.MustChangePassword
            }).ToListAsync();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> GetTutorStatuses()
    {
        var statuses = await db.TutorProfiles
            .AsNoTracking()
            .Select(x => new
            {
                userId = x.ApplicationUserId,
                isActive = x.IsActive && x.ApplicationUser.IsActive,
                setupPending = x.ApplicationUser.MustChangePassword
            })
            .ToListAsync();

        return Json(statuses);
    }


    [HttpGet]
    public async Task<IActionResult> Details(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound();
        }

        var tutor = await db.TutorProfiles
            .AsNoTracking()
            .Where(x => x.ApplicationUserId == userId)
            .Select(x => new TutorDetailsViewModel
            {
                UserId = x.ApplicationUserId,
                UserName = x.ApplicationUser.UserName ?? "",
                FullName = x.FullName,
                Email = x.ApplicationUser.Email ?? "",

                PendingEmail =
                    x.ApplicationUser.PendingTutorEmail,

                PendingEmailRequestId =
                    x.ApplicationUser.TutorEmailChangeRequestId,

                PendingEmailExpiresAtUtc =
                    x.ApplicationUser.TutorEmailChangeExpiresAtUtc,

                PhoneNumber = x.ApplicationUser.PhoneNumber ?? "",
                Qualification = x.Qualification,
                EmploymentType = x.EmploymentType,
                TeachingExperience = x.TeachingExperience,
                AreaOfExpertise = x.AreaOfExpertise ?? "",

                IsActive = x.IsActive && x.ApplicationUser.IsActive,
                SetupPending = x.ApplicationUser.MustChangePassword,

                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (tutor == null)
        {
            return NotFound();
        }

        var teachingSubjects = await db.TutorTeachingSubjects
            .AsNoTracking()
            .Where(x => x.TutorProfile.ApplicationUserId == userId)
            .Select(x => new
            {
                x.EducationStage,
                x.AcademicLevel,
                x.SubjectCode
            })
            .ToListAsync();

        tutor.TeachingLevels = teachingSubjects
            .GroupBy(x => new
            {
                x.EducationStage,
                x.AcademicLevel
            })
            .Select(group => new TutorTeachingLevelDetailsViewModel
            {
                EducationStage = group.Key.EducationStage,
                AcademicLevel = group.Key.AcademicLevel,

                SubjectNames = group
                    .OrderBy(x =>
                        Array.IndexOf(
                            TeachingSubjectCatalog.SubjectDisplayOrder,
                            x.SubjectCode))
                    .Select(x =>
                        TeachingSubjectCatalog.GetSubjectDisplayName(
                            x.SubjectCode))
                    .ToList()
            })
            .OrderBy(x =>
                x.EducationStage.Equals(
                    "Primary",
                    StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x =>
                int.TryParse(
                    x.AcademicLevel.Split(' ').LastOrDefault(),
                    out int number) ? number : 99)
            .ToList();

        var history = await new TutorAccountStatusService(db, users, auditIntegrity)
            .GetHistoryAsync(userId);
        tutor.StatusHistory = history.Items;
        tutor.StatusHistoryHasUnverifiedEntries = history.HasUnverified;

        return View(tutor);
    }

    // ============================================
    // ADMIN - EMAIL CHANGE STATUS
    // ============================================

    [HttpGet]
    [ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> EmailChangeStatus(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var now = DateTime.UtcNow;

        var status = await db.TutorProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.ApplicationUserId == userId)
            .Select(profile => new
            {
                email = profile.ApplicationUser.Email ?? "",

                requestId =
                    profile.ApplicationUser.TutorEmailChangeRequestId,

                pendingEmail =
                    profile.ApplicationUser.PendingTutorEmail,

                pendingExpired =
                    !profile.ApplicationUser
                        .TutorEmailChangeExpiresAtUtc.HasValue
                    || profile.ApplicationUser
                        .TutorEmailChangeExpiresAtUtc.Value <= now,

                isActive =
                    profile.IsActive
                    && profile.ApplicationUser.IsActive
            })
            .FirstOrDefaultAsync();

        if (status == null)
            return NotFound();

        return Json(status);
    }

    // ============================================
    // ADMIN - EDIT TUTOR (GET)
    // ============================================

    [HttpGet]
    public async Task<IActionResult> Edit(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var profile = await db.TutorProfiles
            .AsNoTracking()
            .Include(x => x.ApplicationUser)
            .Include(x => x.TeachingSubjects)
            .FirstOrDefaultAsync(
                x => x.ApplicationUserId == userId);

        if (profile == null)
            return NotFound();

        var model = new EditTutorViewModel
        {
            UserId = profile.ApplicationUserId,
            Email = profile.ApplicationUser.Email ?? "",
            FullName = profile.FullName,
            PhoneNumber = profile.ApplicationUser.PhoneNumber ?? "",
            Qualification = profile.Qualification,
            EmploymentType = profile.EmploymentType,
            AreaOfExpertise = profile.AreaOfExpertise,
            TeachingExperience = profile.TeachingExperience,

            TeachingLevels = profile.TeachingSubjects
                .GroupBy(x => new
                {
                    x.EducationStage,
                    x.AcademicLevel
                })
                .Select(group => new TutorTeachingLevelInput
                {
                    EducationStage = group.Key.EducationStage,
                    AcademicLevel = group.Key.AcademicLevel,
                    SubjectCodes = group
                        .Select(x => x.SubjectCode)
                        .ToList()
                })
                .ToList()
        };

        PopulateTutorEmailDisplay(model, profile.ApplicationUser);

        return View(model);
    }

    // ============================================
    // ADMIN - EDIT TUTOR (POST)
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditTutorViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.UserId))
            return NotFound();

        string? adminUserId = users.GetUserId(User);

        if (string.IsNullOrWhiteSpace(adminUserId))
            return Challenge();

        bool emailChanged = false;
        bool profileChanged = false;

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var profile = await db.TutorProfiles
                .Include(x => x.ApplicationUser)
                .Include(x => x.TeachingSubjects)
                .FirstOrDefaultAsync(
                    x => x.ApplicationUserId == model.UserId);

            if (profile == null)
                return NotFound();

            var tutor = profile.ApplicationUser;

            PopulateTutorEmailDisplay(model, tutor);

            var teachingLevels =
                model.TeachingLevels
                ?? new List<TutorTeachingLevelInput>();

            model.TeachingLevels = teachingLevels;
            model.Email = (model.Email ?? "").Trim();

            ValidateTeachingLevels(new CreateTutorViewModel
            {
                TeachingLevels = teachingLevels
            });

            if (!IsValidTutorEmail(model.Email))
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Please enter a valid email address.");
            }

            if (!TutorAvailabilityPolicy.IsTypeValid(model.EmploymentType))
                ModelState.AddModelError(nameof(model.EmploymentType), "Please select a valid employment type.");
            bool employmentChanged = profile.EmploymentType != model.EmploymentType;
            if (employmentChanged && model.EmploymentType == SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.FullTime)
            {
                var outside = await new TutorAvailabilityService(db).FindExistingClassesOutsideAsync(
                    model.UserId, TutorAvailabilityPolicy.FullTimeSlots());
                if (outside.Count > 0)
                    ModelState.AddModelError(nameof(model.EmploymentType),
                        "Cannot switch to Full-time while scheduled lessons fall outside its fixed hours or within lunch/dinner breaks: "
                        + string.Join("; ", outside.Take(5)) + ". Review the timetable first.");
            }

            if (!ModelState.IsValid)
                return View(model);

            static string Normalize(string? value) =>
                (value ?? "").Trim();

            static string SubjectKey(
                string? stage,
                string? level,
                string? code)
            {
                return string.Join(
                    "|",
                    Normalize(stage).ToUpperInvariant(),
                    Normalize(level).ToUpperInvariant(),
                    Normalize(code).ToUpperInvariant());
            }

            bool personalChanged =
                Normalize(profile.FullName)
                    != Normalize(model.FullName)
                || Normalize(tutor.FullName)
                    != Normalize(model.FullName)
                || Normalize(tutor.PhoneNumber)
                    != Normalize(model.PhoneNumber);

            bool professionalChanged =
                Normalize(profile.Qualification)
                    != Normalize(model.Qualification)
                || Normalize(profile.AreaOfExpertise)
                    != Normalize(model.AreaOfExpertise)
                || Normalize(profile.TeachingExperience)
                    != Normalize(model.TeachingExperience);

            var existingSubjects = profile.TeachingSubjects
                .Select(x => SubjectKey(
                    x.EducationStage,
                    x.AcademicLevel,
                    x.SubjectCode))
                .ToHashSet(StringComparer.Ordinal);

            var submittedSubjects = teachingLevels
                .SelectMany(level =>
                    level.SubjectCodes.Select(code =>
                        SubjectKey(
                            level.EducationStage,
                            level.AcademicLevel,
                            code)))
                .ToHashSet(StringComparer.Ordinal);

            bool subjectsChanged =
                !existingSubjects.SetEquals(submittedSubjects);

            profileChanged =
                personalChanged
                || professionalChanged
                || subjectsChanged
                || employmentChanged;

            emailChanged = !string.Equals(
                users.NormalizeEmail(tutor.Email?.Trim()),
                users.NormalizeEmail(model.Email),
                StringComparison.Ordinal);

            if (!profileChanged && !emailChanged)
            {
                TempData["TutorInfo"] =
                    "No changes were made to the tutor profile.";

                return RedirectToAction(
                    nameof(Details),
                    new { userId = model.UserId });
            }

            if (emailChanged
                && (!tutor.IsActive
                    || !profile.IsActive
                    || !await users.IsInRoleAsync(tutor, "Tutor")))
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email changes are only available for active tutors.");

                return View(model);
            }

            if (profileChanged)
            {
                profile.EmploymentType = model.EmploymentType;
                if (employmentChanged && model.EmploymentType == SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.FullTime)
                    await new TutorAvailabilityService(db).SyncFullTimeSlotsAsync(model.UserId);
                profile.FullName = model.FullName.Trim();
                profile.Qualification = model.Qualification.Trim();
                profile.AreaOfExpertise =
                    model.AreaOfExpertise?.Trim();
                profile.TeachingExperience =
                    model.TeachingExperience?.Trim();
                profile.UpdatedAt = DateTime.UtcNow;

                tutor.FullName = profile.FullName;
                tutor.PhoneNumber = model.PhoneNumber.Trim();
            }

            if (subjectsChanged)
            {
                var requested = teachingLevels
                    .SelectMany(level =>
                        level.SubjectCodes.Select(code => new
                        {
                            Stage = level.EducationStage.Trim(),
                            Level = level.AcademicLevel
                                .Trim()
                                .ToUpperInvariant(),
                            Code = code.Trim().ToUpperInvariant()
                        }))
                    .ToList();

                foreach (var existing
                    in profile.TeachingSubjects.ToList())
                {
                    bool stillSelected = requested.Any(item =>
                        item.Stage.Equals(
                            existing.EducationStage,
                            StringComparison.OrdinalIgnoreCase)
                        && item.Level.Equals(
                            existing.AcademicLevel,
                            StringComparison.OrdinalIgnoreCase)
                        && item.Code.Equals(
                            existing.SubjectCode,
                            StringComparison.OrdinalIgnoreCase));

                    if (!stillSelected)
                    {
                        db.TutorTeachingSubjects.Remove(existing);
                    }
                }

                foreach (var item in requested)
                {
                    bool alreadyExists =
                        profile.TeachingSubjects.Any(existing =>
                            item.Stage.Equals(
                                existing.EducationStage,
                                StringComparison.OrdinalIgnoreCase)
                            && item.Level.Equals(
                                existing.AcademicLevel,
                                StringComparison.OrdinalIgnoreCase)
                            && item.Code.Equals(
                                existing.SubjectCode,
                                StringComparison.OrdinalIgnoreCase));

                    if (!alreadyExists)
                    {
                        db.TutorTeachingSubjects.Add(
                            new TutorTeachingSubject
                            {
                                TutorProfileId = profile.TutorProfileId,
                                EducationStage = item.Stage,
                                AcademicLevel = item.Level,
                                SubjectCode = item.Code
                            });
                    }
                }
            }

            if (emailChanged)
            {
                string? error = await tutorEmailChanges.PrepareAsync(
                    tutor,
                    model.Email,
                    adminUserId);

                if (error != null)
                {
                    ModelState.AddModelError(
                        nameof(model.Email),
                        error);

                    return View(model);
                }
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex) when (
            ex is DbUpdateException
            || ex is System.Data.Common.DbException)
        {
            logger.LogWarning(
                "Tutor edit failed for {UserId}. Error type: {ErrorType}.",
                model.UserId,
                ex.GetType().Name);

            ModelState.AddModelError(
                "",
                "Unable to save the changes. Reload the page and try again.");

            return View(model);
        }

        if (emailChanged)
        {
            bool sent =
                await tutorEmailChanges.SendPendingAsync(model.UserId);

            TempData[sent ? "TutorSuccess" : "TutorWarning"] = sent
                ? (profileChanged
                    ? "Tutor details were saved. "
                    : "")
                    + "A verification email was sent to the new address. "
                    + "The account email will change after confirmation."
                : (profileChanged
                    ? "Tutor details were saved. "
                    : "")
                    + "The email change request was saved, but the "
                    + "verification email could not be sent. "
                    + "Use Resend Verification Email below.";
        }
        else
        {
            TempData["TutorSuccess"] =
                "Tutor details were updated successfully.";
        }

        return RedirectToAction(
            nameof(Details),
            new { userId = model.UserId });
    }


    [HttpGet]
    public IActionResult Create() => View(new CreateTutorViewModel());

    // Admin authorization and antiforgery validation are inherited from this controller.
    [HttpPost]
    public async Task<IActionResult> CheckEmail(string? email)
    {
        email = email?.Trim();
        if (!IsValidTutorEmail(email))
            return Json(new { available = false, message = "Please enter a valid email address." });
        try
        {
            bool exists = await users.FindByEmailAsync(email!) != null;
            return Json(new
            {
                available = !exists,
                message = exists
                    ? "This email is already registered. Please use a different email for the Tutor account."
                    : "This email is available."
            });
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Unable to check this email. Please try again." });
        }
    }
    [HttpPost]
    public async Task<IActionResult> Create(CreateTutorViewModel model)
    {
        // Check a valid Email even when another field is invalid.
        if (!IsValidTutorEmail(model.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Please enter a valid email address.");
        }
        else
        {
            if (await users.FindByEmailAsync(model.Email.Trim()) != null)
                ModelState.AddModelError(nameof(model.Email),
                    "This email is already registered. Please use a different email for the Tutor account.");
        }

        if (!TutorAvailabilityPolicy.IsTypeValid(model.EmploymentType))
            ModelState.AddModelError(nameof(model.EmploymentType), "Please select a valid employment type.");

        // Validate the selected teaching levels and subjects.
        ValidateTeachingLevels(model);

        if (!ModelState.IsValid)
            return View(model);

        model.Email = model.Email.Trim();
        model.FullName = model.FullName.Trim();
        model.Qualification = model.Qualification.Trim();
        model.AreaOfExpertise = model.AreaOfExpertise?.Trim();

        ApplicationUser tutor;
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            try
            {
                // Serializes Tutor ID allocation across application instances.
                await db.Database.ExecuteSqlRawAsync("""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock
                        @Resource = N'EduSmart.TutorProvisioning',
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction',
                        @LockTimeout = 10000;
                    IF @result < 0 THROW 51002, 'Tutor creation is busy. Please retry.', 1;
                    """);
                if (await users.FindByEmailAsync(model.Email) != null)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(nameof(model.Email), "This email is already registered. Please use a different email for the Tutor account.");
                    return View(model);
                }
                tutor = new ApplicationUser
                {
                    UserName = await provisioning.GenerateNextUserIdAsync("TU"),
                    Email = model.Email,
                    PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                    FullName = model.FullName,
                    IsActive = true,
                    MustChangePassword = true,
                    EmailConfirmed = false,
                    LockoutEnabled = true
                };
                var result = await users.CreateAsync(tutor);
                if (result.Succeeded) result = await users.AddToRoleAsync(tutor, "Tutor");
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    foreach (var error in result.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }

                var tutorProfile = new TutorProfile
                {
                    ApplicationUserId = tutor.Id,
                    FullName = model.FullName,
                    Qualification = model.Qualification,
                    EmploymentType = model.EmploymentType,
                    TeachingExperience = model.TeachingExperience?.Trim(),
                    AreaOfExpertise = model.AreaOfExpertise,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Save each selected subject under its academic level.
                foreach (var level in model.TeachingLevels)
                {
                    string educationStage = level.EducationStage.Trim();
                    string academicLevel = level.AcademicLevel.Trim().ToUpperInvariant();

                    foreach (var subjectCode in level.SubjectCodes)
                    {
                        tutorProfile.TeachingSubjects.Add(
                            new TutorTeachingSubject
                            {
                                EducationStage = educationStage,
                                AcademicLevel = academicLevel,
                                SubjectCode = subjectCode.Trim().ToUpperInvariant()
                            });
                    }
                }

                db.TutorProfiles.Add(tutorProfile);
                if (model.EmploymentType == SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.FullTime)
                    await new TutorAvailabilityService(db).SyncFullTimeSlotsAsync(tutor.Id);

                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Tutor creation failed.");
                ModelState.AddModelError(string.Empty, "The tutor could not be created. Refresh the tutor list before retrying.");
                return View(model);
            }
        }

        var sent = await TrySendSetupEmailAsync(tutor);
        TempData[sent ? "TutorSuccess" : "TutorWarning"] = sent
            ? $"Tutor {tutor.UserName} was created and the setup email was sent."
            : $"Tutor {tutor.UserName} was created, but the email could not be sent. Check the email settings, then use Resend setup email. Do not create another account.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResendSetup(string userId)
    {
        var tutor = await users.FindByIdAsync(userId);

        if (tutor == null
            || !await users.IsInRoleAsync(tutor, "Tutor")
            || !await db.TutorProfiles.AnyAsync(profile =>
                profile.ApplicationUserId == userId
                && profile.IsActive))
        {
            return NotFound();
        }

        if (!tutor.IsActive || !tutor.MustChangePassword)
        {
            TempData["TutorWarning"] =
                "Setup email is only available for active tutors "
                + "who have not completed account setup.";

            return RedirectToAction(nameof(Index));
        }

        if (tutor.TutorEmailChangeRequestId.HasValue
            || !string.IsNullOrWhiteSpace(tutor.PendingTutorEmail))
        {
            TempData["TutorWarning"] =
                "An email change is pending. Complete the new "
                + "email verification first. If the verification "
                + "link has expired, request a new verification email.";

            return RedirectToAction(nameof(Index));
        }

        bool sent = await TrySendSetupEmailAsync(tutor);

        TempData[sent ? "TutorSuccess" : "TutorWarning"] = sent
            ? $"Setup email sent to {tutor.Email}."
            : "The setup email could not be sent. "
                + "Check the email settings and private server log.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // PUBLIC - REVIEW TUTOR EMAIL CHANGE
    // ============================================

    [AllowAnonymous]
    [HttpGet]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(
        "PublicFormPolicy")]
    public async Task<IActionResult> ConfirmEmailChange(
        string? userId,
        Guid? requestId,
        string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (string.IsNullOrWhiteSpace(userId)
            || userId.Length > 450
            || !requestId.HasValue
            || requestId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(token)
            || token.Length > 8192)
        {
            return InvalidEmailChangeView();
        }

        var tutor = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId);

        if (!await IsCurrentTutorEmailRequestAsync(
                tutor,
                requestId.Value))
        {
            return InvalidEmailChangeView();
        }

        string? decodedToken = DecodeEmailChangeToken(token);

        if (decodedToken == null)
        {
            return InvalidEmailChangeView();
        }

        bool tokenValid = await users.VerifyUserTokenAsync(
            tutor!,
            users.Options.Tokens.ChangeEmailTokenProvider,
            "ChangeEmail:" + tutor!.PendingTutorEmail,
            decodedToken);

        if (!tokenValid)
        {
            return InvalidEmailChangeView();
        }

        return View(
            "ConfirmEmailChange",
            new ConfirmTutorEmailChangeViewModel
            {
                UserId = tutor.Id,
                RequestId = requestId.Value,
                Token = token,
                TutorId = tutor.UserName ?? "",
                NewEmail = tutor.PendingTutorEmail!,
                CanConfirm = true
            });
    }

    // ============================================
    // PUBLIC - CONFIRM TUTOR EMAIL CHANGE
    // ============================================

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(
        "PublicFormPolicy")]
    public async Task<IActionResult> ConfirmEmailChange(
        ConfirmTutorEmailChangeViewModel model)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (!ModelState.IsValid
            || string.IsNullOrWhiteSpace(model.UserId)
            || model.UserId.Length > 450
            || model.RequestId == Guid.Empty
            || string.IsNullOrWhiteSpace(model.Token)
            || model.Token.Length > 8192)
        {
            return InvalidEmailChangeView();
        }

        string? decodedToken =
            DecodeEmailChangeToken(model.Token);

        if (decodedToken == null)
        {
            return InvalidEmailChangeView();
        }

        ApplicationUser? confirmedTutor = null;

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var tutor = await db.Users
                .FirstOrDefaultAsync(
                    user => user.Id == model.UserId);

            if (!await IsCurrentTutorEmailRequestAsync(
                    tutor,
                    model.RequestId))
            {
                return InvalidEmailChangeView();
            }

            // Always use the email stored by the Admin request.
            // Never accept a replacement email from this form.
            string newEmail = tutor!.PendingTutorEmail!;

            if (!IsValidTutorEmail(newEmail))
            {
                return InvalidEmailChangeView();
            }

            string? normalizedNewEmail =
                users.NormalizeEmail(newEmail);

            bool emailAlreadyUsed = await db.Users
                .AnyAsync(user =>
                    user.Id != tutor.Id
                    && user.NormalizedEmail == normalizedNewEmail);

            if (emailAlreadyUsed)
            {
                return InvalidEmailChangeView(
                    "This email is now used by another account. "
                    + "Please contact your administrator "
                    + "to request a different email address.");
            }

            // These changes are saved together by ChangeEmailAsync.
            tutor.PendingTutorEmail = null;
            tutor.TutorEmailChangeRequestId = null;
            tutor.TutorEmailChangeRequestedAtUtc = null;
            tutor.TutorEmailChangeExpiresAtUtc = null;
            tutor.TutorEmailChangeRequestedByUserId = null;

            var result = await users.ChangeEmailAsync(
                tutor,
                newEmail,
                decodedToken);

            if (!result.Succeeded)
            {
                return InvalidEmailChangeView();
            }

            await transaction.CommitAsync();

            confirmedTutor = tutor;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(
                "Tutor email confirmation failed for {UserId}. "
                + "Error type: {ErrorType}.",
                model.UserId,
                ex.GetType().Name);

            return InvalidEmailChangeView(
                "The email change could not be completed. "
                + "Please open the verification link again "
                + "or contact your administrator.");
        }

        string infoMessage;

        if (confirmedTutor!.MustChangePassword)
        {
            bool setupEmailSent =
                await TrySendSetupEmailAsync(confirmedTutor);

            infoMessage = setupEmailSent
                ? "An account setup email has been sent to your "
                    + "new email address. Please use its link "
                    + "to create your password before signing in."
                : "Your email was updated, but the account setup "
                    + "email could not be sent. Please ask your "
                    + "administrator to resend the setup email.";
        }
        else
        {
            infoMessage =
                "Your Tutor ID and password remain unchanged. "
                + "You can sign in using your Tutor ID "
                + "or your new email address. "
                + "Existing sessions will require you to sign in again.";
        }

        ModelState.Clear();

        return View(
            "ConfirmEmailChange",
            new ConfirmTutorEmailChangeViewModel
            {
                TutorId = confirmedTutor.UserName ?? "",
                NewEmail = confirmedTutor.Email ?? "",
                Confirmed = true,
                CanConfirm = false,
                InfoMessage = infoMessage
            });
    }

    private async Task<bool> IsCurrentTutorEmailRequestAsync(
        ApplicationUser? tutor,
        Guid requestId)
    {
        if (tutor == null
            || !tutor.IsActive
            || requestId == Guid.Empty
            || tutor.TutorEmailChangeRequestId != requestId
            || string.IsNullOrWhiteSpace(tutor.PendingTutorEmail)
            || !tutor.TutorEmailChangeExpiresAtUtc.HasValue
            || tutor.TutorEmailChangeExpiresAtUtc.Value
                <= DateTime.UtcNow)
        {
            return false;
        }

        if (!await users.IsInRoleAsync(tutor, "Tutor"))
        {
            return false;
        }

        return await db.TutorProfiles.AnyAsync(profile =>
            profile.ApplicationUserId == tutor.Id
            && profile.IsActive);
    }

    private IActionResult InvalidEmailChangeView(
        string? message = null)
    {
        ModelState.Clear();

        return View(
            "ConfirmEmailChange",
            new ConfirmTutorEmailChangeViewModel
            {
                CanConfirm = false,
                Confirmed = false,
                ErrorMessage = message
                    ?? "This verification link is invalid, "
                        + "expired or has already been used."
            });
    }

    private static string? DecodeEmailChangeToken(
        string encodedToken)
    {
        try
        {
            byte[] tokenBytes =
                WebEncoders.Base64UrlDecode(encodedToken);

            return Encoding.UTF8.GetString(tokenBytes);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsValidTutorEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim();

        if (email.Length > 254)
            return false;

        if (!new EmailAddressAttribute().IsValid(email))
            return false;

        return Regex.IsMatch(
            email,
            @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@" +
            @"(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+" +
            @"[A-Za-z]{2,63}$",
            RegexOptions.CultureInvariant);
    }

    private void ValidateTeachingLevels(CreateTutorViewModel model)
    {
        if (model.TeachingLevels == null ||
            model.TeachingLevels.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.TeachingLevels),
                "Please select at least one academic level.");

            return;
        }

        var selectedLevels = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < model.TeachingLevels.Count; i++)
        {
            var level = model.TeachingLevels[i];

            if (level == null)
            {
                ModelState.AddModelError(
                    nameof(model.TeachingLevels),
                    "Invalid teaching level information.");
                continue;
            }

            string educationStage =
                level.EducationStage?.Trim() ?? "";

            string academicLevel =
                level.AcademicLevel?.Trim().ToUpperInvariant() ?? "";

            // Validate Education Stage and Academic Level.
            if (!TeachingSubjectCatalog.IsValidStageAndLevel(
                    educationStage, academicLevel))
            {
                ModelState.AddModelError(
                    $"TeachingLevels[{i}].AcademicLevel",
                    "The academic level does not match the education stage.");

                continue;
            }

            // Prevent duplicate academic levels.
            if (!selectedLevels.Add(academicLevel))
            {
                ModelState.AddModelError(
                    $"TeachingLevels[{i}].AcademicLevel",
                    "This academic level has already been selected.");
            }

            // Each selected level must have at least one subject.
            if (level.SubjectCodes == null ||
                level.SubjectCodes.Count == 0)
            {
                ModelState.AddModelError(
                    $"TeachingLevels[{i}].SubjectCodes",
                    "Please select at least one subject for this level.");

                continue;
            }

            var selectedSubjects = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var subject in level.SubjectCodes)
            {
                string subjectCode =
                    subject?.Trim().ToUpperInvariant() ?? "";

                // Prevent duplicate subjects.
                if (!selectedSubjects.Add(subjectCode))
                {
                    ModelState.AddModelError(
                        $"TeachingLevels[{i}].SubjectCodes",
                        "Duplicate subjects are not allowed.");

                    continue;
                }

                // Validate subject against academic level.
                if (!TeachingSubjectCatalog.IsValidSubject(
                        academicLevel, subjectCode))
                {
                    ModelState.AddModelError(
                        $"TeachingLevels[{i}].SubjectCodes",
                        $"Subject {subjectCode} is not available for {academicLevel}.");
                }
            }
        }
    }

    private async Task<bool> TrySendSetupEmailAsync(
        ApplicationUser tutor)
    {
        try
        {
            var configuredUrl =
                configuration["App:PublicBaseUrl"];

            if (string.IsNullOrWhiteSpace(configuredUrl) &&
                environment.IsDevelopment())
            {
                configuredUrl = "https://localhost:7086";
            }

            if (!Uri.TryCreate(
                    configuredUrl,
                    UriKind.Absolute,
                    out var baseUri) ||
                baseUri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(baseUri.UserInfo) ||
                baseUri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(baseUri.Query) ||
                !string.IsNullOrEmpty(baseUri.Fragment) ||
                (!environment.IsDevelopment() && baseUri.IsLoopback))
            {
                throw new InvalidOperationException(
                    "App:PublicBaseUrl must be the public HTTPS origin.");
            }

            var token =
                await users.GeneratePasswordResetTokenAsync(tutor);

            var setupUrl =
                QueryHelpers.AddQueryString(
                    baseUri.GetLeftPart(UriPartial.Authority)
                        + "/Identity/Account/SetupPassword",
                    new Dictionary<string, string?>
                    {
                        ["userId"] = tutor.Id,
                        ["token"] = WebEncoders.Base64UrlEncode(
                            Encoding.UTF8.GetBytes(token))
                    });

            await tutorEmailService.SendAsync(
                recipientEmail: tutor.Email!,
                tutorName: tutor.FullName ?? "",
                tutorUserId: tutor.UserName ?? "",
                setupUrl: setupUrl);

            return true;
        }
        catch (Exception ex)
        {
            // Never log setup tokens, URLs or SMTP credentials.
            logger.LogWarning(
                "Tutor setup email failed for {UserId}. Error type: {ErrorType}.",
                tutor.Id,
                ex.GetType().Name);

            return false;
        }
    }

    private static void PopulateTutorEmailDisplay(
    EditTutorViewModel model,
    ApplicationUser tutor)
    {
        model.TutorId = tutor.UserName ?? "";
        model.CurrentEmail = tutor.Email ?? "";
        model.PendingEmail = tutor.PendingTutorEmail;
        model.PendingEmailRequestId =
            tutor.TutorEmailChangeRequestId;
        model.PendingEmailExpiresAtUtc =
            tutor.TutorEmailChangeExpiresAtUtc;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailVerification(
        string userId,
        Guid requestId)
    {
        string? adminUserId = users.GetUserId(User);

        if (string.IsNullOrWhiteSpace(adminUserId))
            return Challenge();

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var tutor = await db.Users
                .FirstOrDefaultAsync(user => user.Id == userId);

            if (tutor == null
                || !tutor.IsActive
                || !await users.IsInRoleAsync(tutor, "Tutor")
                || !await db.TutorProfiles.AnyAsync(profile =>
                    profile.ApplicationUserId == userId
                    && profile.IsActive))
            {
                return NotFound();
            }

            if (requestId == Guid.Empty
                || tutor.TutorEmailChangeRequestId != requestId
                || string.IsNullOrWhiteSpace(tutor.PendingTutorEmail))
            {
                TempData["TutorWarning"] =
                    "The pending request has changed. Please reload the page.";

                return RedirectToAction(
                    nameof(Details),
                    new { userId });
            }

            if (tutor.TutorEmailChangeRequestedAtUtc.HasValue
                && tutor.TutorEmailChangeRequestedAtUtc.Value
                    > DateTime.UtcNow.AddMinutes(-1))
            {
                TempData["TutorInfo"] =
                    "Please wait one minute before resending.";

                return RedirectToAction(
                    nameof(Details),
                    new { userId });
            }

            string? error = await tutorEmailChanges.PrepareAsync(
                tutor,
                tutor.PendingTutorEmail!,
                adminUserId);

            if (error != null)
            {
                TempData["TutorWarning"] = error;

                return RedirectToAction(
                    nameof(Details),
                    new { userId });
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex) when (
            ex is DbUpdateException
            || ex is System.Data.Common.DbException)
        {
            logger.LogWarning(
                "Tutor verification resend failed for {UserId}. "
                + "Error type: {ErrorType}.",
                userId,
                ex.GetType().Name);

            TempData["TutorWarning"] =
                "Unable to renew the verification request. Please try again.";

            return RedirectToAction(
                nameof(Details),
                new { userId });
        }

        bool sent =
            await tutorEmailChanges.SendPendingAsync(userId);

        TempData[sent ? "TutorSuccess" : "TutorWarning"] = sent
            ? "A new verification email was sent. "
                + "Previous verification links are no longer valid."
            : "The request was renewed, but the email could not be sent. "
                + "Check the email settings and try again.";

        return RedirectToAction(
            nameof(Details),
            new { userId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEmailChange(
        string userId,
        Guid requestId)
    {
        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var tutor = await db.Users
                .FirstOrDefaultAsync(user => user.Id == userId);

            if (tutor == null
                || !await users.IsInRoleAsync(tutor, "Tutor"))
            {
                return NotFound();
            }

            if (requestId == Guid.Empty
                || tutor.TutorEmailChangeRequestId != requestId)
            {
                TempData["TutorWarning"] =
                    "The pending request has changed. Please reload the page.";

                return RedirectToAction(
                    nameof(Details),
                    new { userId });
            }

            tutor.PendingTutorEmail = null;
            tutor.TutorEmailChangeRequestId = null;
            tutor.TutorEmailChangeRequestedAtUtc = null;
            tutor.TutorEmailChangeExpiresAtUtc = null;
            tutor.TutorEmailChangeRequestedByUserId = null;

            var result = await users.UpdateAsync(tutor);

            if (!result.Succeeded)
            {
                TempData["TutorWarning"] =
                    "Unable to cancel the request. Please try again.";

                return RedirectToAction(
                    nameof(Details),
                    new { userId });
            }

            await transaction.CommitAsync();

            TempData["TutorSuccess"] =
                "The email change request was cancelled. "
                + "The current account email is unchanged.";

            if (tutor.MustChangePassword)
            {
                TempData["TutorInfo"] =
                    "Previous setup links remain invalid. "
                    + "Check the current email address, then use "
                    + "Resend setup email from Tutor Management.";
            }
        }
        catch (Exception ex) when (
            ex is DbUpdateException
            || ex is System.Data.Common.DbException)
        {
            logger.LogWarning(
                "Tutor email cancellation failed for {UserId}. "
                + "Error type: {ErrorType}.",
                userId,
                ex.GetType().Name);

            TempData["TutorWarning"] =
                "Unable to cancel the request. Please try again.";
        }

        return RedirectToAction(
            nameof(Details),
            new { userId });
    }
}


