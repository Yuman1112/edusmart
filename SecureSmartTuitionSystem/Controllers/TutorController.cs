using System.Data;
using Microsoft.AspNetCore.DataProtection;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Services.Enrollment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Tutor")]
    public class TutorController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;


        public TutorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IDataProtectionProvider protectionProvider,
            ILogger<TutorController> profileLogger,
            TutorEmailChangeService profileEmailChanges)
        {
            _context = context;
            _userManager = userManager;
            _emailWatchProtector = protectionProvider.CreateProtector(
                "EduSmart.Tutor.EmailChangeWatch.v1");
            _profileLogger = profileLogger;
            _profileEmailChanges = profileEmailChanges;
        }

        private readonly IDataProtector _emailWatchProtector;
        private readonly ILogger<TutorController> _profileLogger;
        private readonly TutorEmailChangeService _profileEmailChanges;


        /*
         * =====================================================
         * TUTOR DASHBOARD
         * =====================================================
         */

        private IQueryable<StudentEnrollment> CurrentRoster(string tutorId)
        {
            var today = ClassScheduleService.Today;
            return _context.StudentEnrollments.AsNoTracking().Where(e =>
                e.ClassSession.TutorUserId == tutorId &&
                e.ClassSession.Status == ClassSessionStatus.Active &&
                (e.ClassSession.StartDate == null || e.ClassSession.StartDate <= today) &&
                (e.ClassSession.EndDate == null || e.ClassSession.EndDate >= today) &&
                e.StudentProfile.IsActive && e.StartDate <= today && e.StartDate.Year == today.Year &&
                (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop) &&
                (e.DropEffectiveDate == null || e.DropEffectiveDate > today) &&
                (e.EndedAt == null || e.EndedAt > today));
        }

        [HttpGet]
        public async Task<IActionResult> Students(int? classId)
        {
            var tutorId = _userManager.GetUserId(User);
            if (tutorId == null) return Challenge();

            var classes = await _context.ClassSessions.AsNoTracking()
                .Where(c => c.TutorUserId == tutorId && c.Status == ClassSessionStatus.Active)
                .OrderBy(c => c.ClassCode)
                .Select(c => new TutorRosterClass(c.ClassSessionId, c.ClassCode, c.ClassName ?? ""))
                .ToListAsync();

            if (classId.HasValue && !classes.Any(c => c.Id == classId.Value))
                return NotFound();

            var roster = await CurrentRoster(tutorId)
                .Where(e => !classId.HasValue || e.ClassSessionId == classId.Value)
                .Select(e => new
                {
                    Number = e.StudentProfile.StudentNumber,
                    Name = e.StudentProfile.FullName,
                    e.ClassSession.ClassCode,
                    ClassName = e.ClassSession.ClassName ?? ""
                })
                .Distinct().OrderBy(e => e.Name).ThenBy(e => e.Number).ThenBy(e => e.ClassCode)
                .Select(e => new TutorRosterStudent(e.Number, e.Name, e.ClassCode, e.ClassName))
                .ToListAsync();
            return View(new TutorStudentRoster(classId, classes, roster));
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            var activeClasses =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        session.TutorUserId == tutor.Id &&
                        session.Status ==
                            ClassSessionStatus.Active)
                    .OrderBy(session =>
                        session.DayOfWeek ==
                            DayOfWeek.Monday ? 1 :
                        session.DayOfWeek ==
                            DayOfWeek.Tuesday ? 2 :
                        session.DayOfWeek ==
                            DayOfWeek.Wednesday ? 3 :
                        session.DayOfWeek ==
                            DayOfWeek.Thursday ? 4 :
                        session.DayOfWeek ==
                            DayOfWeek.Friday ? 5 :
                        session.DayOfWeek ==
                            DayOfWeek.Saturday ? 6 :
                        session.DayOfWeek ==
                            DayOfWeek.Sunday ? 7 : 8)
                    .ThenBy(session =>
                        session.StartTime)
                    .ToListAsync();

            var classIds =
                activeClasses
                    .Select(session =>
                        session.ClassSessionId)
                    .ToList();

            var studentCounts =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment =>
                        classIds.Contains(
                            enrollment.ClassSessionId) &&
                        (enrollment.Status == StudentEnrollmentStatus.Active || (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > ClassScheduleService.Today))))
                    .GroupBy(enrollment =>
                        enrollment.ClassSessionId)
                    .Select(group =>
                        new
                        {
                            ClassSessionId = group.Key,
                            StudentCount = group.Count()
                        })
                    .ToDictionaryAsync(
                        item => item.ClassSessionId,
                        item => item.StudentCount);

            int activeStudentCount =
                await CurrentRoster(tutor.Id)
                    .Select(enrollment =>
                        enrollment.StudentProfileId)
                    .Distinct()
                    .CountAsync();

            var model =
                new TutorDashboardViewModel
                {
                    TutorName =
                        tutor.FullName ??
                        tutor.Email ??
                        "Tutor",

                    AssignedClassCount =
                        activeClasses.Count,

                    TodayClassCount =
                        (await new ClassScheduleService(_context).GetLessonsAsync(ClassScheduleService.Today, ClassScheduleService.Today, tutor.Id)).Count(x => !x.Cancelled),

                    ActiveStudentCount =
                        activeStudentCount,

                    Classes =
                        activeClasses
                            .Select(session =>
                                new TutorClassItemViewModel
                                {
                                    ClassSessionId =
                                        session.ClassSessionId,

                                    ClassCode =
                                        session.ClassCode,

                                    SubjectName =
                                        session.Subject.SubjectName,

                                    AcademicLevel =
                                        session.Subject.AcademicLevel,

                                    Day =
                                        session.DayOfWeek.ToString(),

                                    Time =
                                        session.StartTime
                                            .ToString("hh:mm tt") +
                                        " - " +
                                        session.EndTime
                                            .ToString("hh:mm tt"),

                                    Venue =
                                        session.Venue,

                                    StudentCount =
                                        studentCounts.TryGetValue(
                                            session.ClassSessionId,
                                            out int count)
                                                ? count
                                                : 0
                                })
                            .ToList()
                };

            return View(model);
        }


        /*
         * =====================================================
         * TUTOR MY CLASSES
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> MyClasses(
            string? subjectName,
            DayOfWeek? dayOfWeek,
            ClassSessionStatus? status)
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            subjectName =
                string.IsNullOrWhiteSpace(subjectName)
                    ? null
                    : subjectName.Trim();

            /*
             * This query always limits the records to the
             * currently logged-in Tutor.
             */
            var tutorClassesQuery =
                _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        session.TutorUserId == tutor.Id);

            /*
             * Summary values use all classes assigned
             * to the Tutor, before filters are applied.
             */
            int totalClassCount =
                await tutorClassesQuery.CountAsync();

            int activeClassCount =
                await tutorClassesQuery.CountAsync(session =>
                    session.Status ==
                        ClassSessionStatus.Active);

            int todayClassCount = (await new ClassScheduleService(_context).GetLessonsAsync(ClassScheduleService.Today, ClassScheduleService.Today, tutor.Id)).Count(x => !x.Cancelled);

            /*
             * Subject filter options only include subjects
             * belonging to this Tutor's assigned classes.
             */
            var subjectOptions =
                await tutorClassesQuery
                    .Select(session =>
                        session.Subject.SubjectName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync();

            /*
             * Apply filters.
             */
            if (subjectName != null)
            {
                tutorClassesQuery =
                    tutorClassesQuery.Where(session =>
                        session.Subject.SubjectName ==
                            subjectName);
            }

            if (dayOfWeek.HasValue)
            {
                tutorClassesQuery =
                    tutorClassesQuery.Where(session =>
                        session.DayOfWeek ==
                            dayOfWeek.Value);
            }

            if (status.HasValue)
            {
                tutorClassesQuery =
                    tutorClassesQuery.Where(session =>
                        session.Status ==
                            status.Value);
            }

            /*
             * Monday to Sunday schedule order.
             */
            var assignedClasses =
                await tutorClassesQuery
                    .OrderBy(session =>
                        session.Status)
                    .ThenBy(session =>
                        session.DayOfWeek ==
                            DayOfWeek.Monday ? 1 :
                        session.DayOfWeek ==
                            DayOfWeek.Tuesday ? 2 :
                        session.DayOfWeek ==
                            DayOfWeek.Wednesday ? 3 :
                        session.DayOfWeek ==
                            DayOfWeek.Thursday ? 4 :
                        session.DayOfWeek ==
                            DayOfWeek.Friday ? 5 :
                        session.DayOfWeek ==
                            DayOfWeek.Saturday ? 6 :
                        session.DayOfWeek ==
                            DayOfWeek.Sunday ? 7 : 8)
                    .ThenBy(session =>
                        session.StartTime)
                    .ToListAsync();

            var classIds =
                assignedClasses
                    .Select(session =>
                        session.ClassSessionId)
                    .ToList();

            var studentCounts =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment =>
                        classIds.Contains(
                            enrollment.ClassSessionId) &&
                        (enrollment.Status == StudentEnrollmentStatus.Active || (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > ClassScheduleService.Today))))
                    .GroupBy(enrollment =>
                        enrollment.ClassSessionId)
                    .Select(group =>
                        new
                        {
                            ClassSessionId = group.Key,
                            StudentCount = group.Count()
                        })
                    .ToDictionaryAsync(
                        item => item.ClassSessionId,
                        item => item.StudentCount);

            var model =
                new TutorMyClassesViewModel
                {
                    TutorName =
                        tutor.FullName ??
                        tutor.Email ??
                        "Tutor",

                    TotalClassCount =
                        totalClassCount,

                    ActiveClassCount =
                        activeClassCount,

                    TodayClassCount =
                        todayClassCount,

                    SubjectName =
                        subjectName,

                    DayOfWeek =
                        dayOfWeek,

                    Status =
                        status,

                    SubjectOptions =
                        subjectOptions,

                    Classes =
                        assignedClasses
                            .Select(session =>
                                new TutorMyClassItemViewModel
                                {
                                    ClassSessionId =
                                        session.ClassSessionId,

                                    ClassCode =
                                        session.ClassCode,

                                    ClassName =
                                        session.ClassName ??
                                        session.Subject
                                            .AcademicLevel +
                                        " - " +
                                        session.Subject
                                            .SubjectName,

                                    EducationStage =
                                        session.Subject
                                            .EducationStage
                                            .ToString(),

                                    AcademicLevel =
                                        session.Subject
                                            .AcademicLevel,

                                    SubjectName =
                                        session.Subject
                                            .SubjectName,

                                    Day =
                                        session.DayOfWeek
                                            .ToString(),

                                    StartTime =
                                        session.StartTime
                                            .ToString("hh:mm tt"),

                                    EndTime =
                                        session.EndTime
                                            .ToString("hh:mm tt"),

                                    GoogleMeetLink =
                                        session.Venue,

                                    Capacity =
                                        session.Capacity,

                                    StudentCount =
                                        studentCounts
                                            .TryGetValue(
                                                session
                                                    .ClassSessionId,
                                                out int count)
                                                    ? count
                                                    : 0,

                                    Status =
                                        session.Status
                                })
                            .ToList()
                };

            return View(model);
        }


        /*
         * =====================================================
         * TUTOR CLASS DETAILS
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> ClassDetails(
            int id)
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            /*
             * TutorUserId is included in the query.
             * Therefore, a Tutor cannot view another
             * Tutor's class by changing the URL ID.
             */
            var session =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(item => item.Subject)
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id &&
                        item.TutorUserId == tutor.Id);

            if (session == null)
            {
                return NotFound();
            }

            int studentCount =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .CountAsync(enrollment =>
                        enrollment.ClassSessionId ==
                            session.ClassSessionId &&
                        (enrollment.Status == StudentEnrollmentStatus.Active || (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > ClassScheduleService.Today))));

            var model =
                new TutorClassDetailsViewModel
                {
                    ClassSessionId =
                        session.ClassSessionId,

                    ClassCode =
                        session.ClassCode,

                    ClassName =
                        session.ClassName ??
                        session.Subject.AcademicLevel +
                        " - " +
                        session.Subject.SubjectName,

                    EducationStage =
                        session.Subject
                            .EducationStage
                            .ToString(),

                    AcademicLevel =
                        session.Subject.AcademicLevel,

                    SubjectName =
                        session.Subject.SubjectName,

                    Day =
                        session.DayOfWeek.ToString(),

                    StartTime =
                        session.StartTime
                            .ToString("hh:mm tt"),

                    EndTime =
                        session.EndTime
                            .ToString("hh:mm tt"),

                    GoogleMeetLink =
                        session.Venue,

                    Capacity =
                        session.Capacity,

                    StudentCount =
                        studentCount,

                    Remark =
                        session.Remark,

                    Status =
                        session.Status
                };

            return View(model);
        }

        // ============================================
        // TUTOR - MY PROFILE
        // ============================================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> MyProfile()
        {
            var profile = await GetOwnTutorProfileAsync(includeTeachingSubjects: true);
            if (profile == null) return NotFound();

            var user = profile.ApplicationUser;
            var model = new TutorDetailsViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? "",
                FullName = profile.FullName,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                Qualification = profile.Qualification,
                FieldOfStudy = profile.FieldOfStudy,
                AreaOfExpertise = profile.AreaOfExpertise ?? "",
                TeachingExperience = profile.TeachingExperience,
                IsActive = profile.IsActive && user.IsActive,
                SetupPending = user.MustChangePassword,
                PendingEmail = user.PendingTutorEmail,
                PendingEmailRequestId = user.TutorEmailChangeRequestId,
                PendingEmailExpiresAtUtc = user.TutorEmailChangeExpiresAtUtc,
                CreatedAt = profile.CreatedAt,
                UpdatedAt = profile.UpdatedAt,
                TeachingLevels = profile.TeachingSubjects
                    .GroupBy(x => new { x.EducationStage, x.AcademicLevel })
                    .Select(group => new TutorTeachingLevelDetailsViewModel
                    {
                        EducationStage = group.Key.EducationStage,
                        AcademicLevel = group.Key.AcademicLevel,
                        SubjectNames = group
                            .OrderBy(x => Array.IndexOf(
                                TeachingSubjectCatalog.SubjectDisplayOrder,
                                x.SubjectCode))
                            .Select(x => TeachingSubjectCatalog
                                .GetSubjectDisplayName(x.SubjectCode))
                            .ToList()
                    })
                    .OrderBy(x => x.EducationStage == "Primary" ? 0 : 1)
                    .ThenBy(x => int.TryParse(
                        x.AcademicLevel.Split(' ').LastOrDefault(),
                        out int number) ? number : 99)
                    .ToList()
            };

            if (user.TutorEmailChangeRequestId.HasValue
                && !string.IsNullOrWhiteSpace(user.PendingTutorEmail))
            {
                var watchData = new[]
                {
                    user.Id,
                    user.TutorEmailChangeRequestId.Value.ToString("D"),
                    _userManager.NormalizeEmail(
                        user.PendingTutorEmail) ?? "",
                    DateTime.UtcNow.AddHours(2).Ticks.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                };

                model.EmailChangeWatchToken =
                    _emailWatchProtector.Protect(
                        System.Text.Json.JsonSerializer.Serialize(
                            watchData));
            }

            return View(model);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> EditProfile()
        {
            var profile = await GetOwnTutorProfileAsync();
            if (profile == null) return NotFound();

            var model = new EditOwnTutorProfileViewModel
            {
                TutorId = profile.ApplicationUser.UserName ?? "",
                Email = profile.ApplicationUser.Email ?? "",
                FullName = profile.FullName,
                PhoneNumber = profile.ApplicationUser.PhoneNumber ?? ""
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> EditProfile(
            EditOwnTutorProfileViewModel model)
        {
            bool emailChanged = false;
            bool profileChanged = false;

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var profile = await GetOwnTutorProfileAsync(tracking: true);
                if (profile == null) return NotFound();

                var user = profile.ApplicationUser;

                // Account ownership comes from the signed-in user.
                model.TutorId = user.UserName ?? "";
                ModelState.Remove(nameof(model.TutorId));
                model.Email = (model.Email ?? "").Trim();

                if (!IsValidOwnTutorEmail(model.Email))
                {
                    ModelState.AddModelError(nameof(model.Email),
                        "Please enter a valid email address.");
                }

                if (!ModelState.IsValid) return View(model);

                emailChanged = !string.Equals(
                    _userManager.NormalizeEmail(user.Email?.Trim()),
                    _userManager.NormalizeEmail(model.Email),
                    StringComparison.Ordinal);

                static string Clean(string? value) => (value ?? "").Trim();

                profileChanged = Clean(profile.FullName) != Clean(model.FullName)
                    || Clean(user.FullName) != Clean(model.FullName)
                    || Clean(user.PhoneNumber) != Clean(model.PhoneNumber);

                if (!profileChanged && !emailChanged)
                {
                    TempData["OwnProfileInfo"] = "No changes were made to your profile.";
                    return RedirectToAction(nameof(MyProfile));
                }

                if (profileChanged)
                {
                    profile.FullName = model.FullName.Trim();
                    profile.UpdatedAt = DateTime.UtcNow;
                    user.FullName = profile.FullName;

                    string phone = model.PhoneNumber.Trim();
                    if (!string.Equals(user.PhoneNumber, phone, StringComparison.Ordinal))
                    {
                        user.PhoneNumber = phone;
                        user.PhoneNumberConfirmed = false;
                    }
                }

                if (emailChanged)
                {
                    string? error = await _profileEmailChanges.PrepareAsync(
                        user, model.Email, user.Id);

                    if (error != null)
                    {
                        ModelState.AddModelError(nameof(model.Email), error);
                        return View(model);
                    }
                }
                else
                {
                    var result = await _userManager.UpdateAsync(user);
                    if (!result.Succeeded)
                    {
                        ModelState.AddModelError("",
                            "Unable to save your profile. Reload the page and try again.");
                        return View(model);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex) when (
                ex is DbUpdateException
                || ex is System.Data.Common.DbException)
            {
                _profileLogger.LogWarning(
                    "Tutor self-profile update failed for {UserId}. Error type: {ErrorType}.",
                    _userManager.GetUserId(User), ex.GetType().Name);

                ModelState.AddModelError("",
                    "Unable to save your profile. Reload the page and try again.");
                return View(model);
            }

            if (emailChanged)
            {
                string userId = _userManager.GetUserId(User)!;
                bool sent = await _profileEmailChanges.SendPendingAsync(userId);
                string prefix = profileChanged ? "Your profile was saved. " : "";

                TempData[sent ? "OwnProfileSuccess" : "OwnProfileWarning"] = sent
                    ? prefix + "A verification email was sent to the new address. "
                        + "Your current email remains unchanged until confirmation."
                    : prefix + "The email change request was saved, but the email "
                        + "could not be sent. Use Resend Verification Email.";
            }
            else
            {
                TempData["OwnProfileSuccess"] = "Your profile was updated successfully.";
            }

            return RedirectToAction(nameof(MyProfile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOwnEmailVerification(
    Guid requestId)
        {
            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var profile =
                    await GetOwnTutorProfileAsync(tracking: true);

                if (profile == null)
                    return NotFound();

                var user = profile.ApplicationUser;

                if (requestId == Guid.Empty
                    || user.TutorEmailChangeRequestId != requestId
                    || string.IsNullOrWhiteSpace(user.PendingTutorEmail))
                {
                    TempData["OwnProfileWarning"] =
                        "The pending request has changed. "
                        + "Please reload the page.";

                    return RedirectToAction(nameof(MyProfile));
                }

                if (user.TutorEmailChangeRequestedAtUtc.HasValue
                    && user.TutorEmailChangeRequestedAtUtc.Value
                        > DateTime.UtcNow.AddMinutes(-1))
                {
                    TempData["OwnProfileInfo"] =
                        "Please wait one minute before resending.";

                    return RedirectToAction(nameof(MyProfile));
                }

                string? error =
                    await _profileEmailChanges.PrepareAsync(
                        user,
                        user.PendingTutorEmail!,
                        user.Id);

                if (error != null)
                {
                    TempData["OwnProfileWarning"] = error;
                    return RedirectToAction(nameof(MyProfile));
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex) when (
                ex is DbUpdateException
                || ex is System.Data.Common.DbException)
            {
                _profileLogger.LogWarning(
                    "Tutor self email resend failed for {UserId}. "
                    + "Error type: {ErrorType}.",
                    _userManager.GetUserId(User),
                    ex.GetType().Name);

                TempData["OwnProfileWarning"] =
                    "Unable to renew the request. Please try again.";

                return RedirectToAction(nameof(MyProfile));
            }

            bool sent = await _profileEmailChanges.SendPendingAsync(
                _userManager.GetUserId(User)!);

            TempData[sent ? "OwnProfileSuccess" : "OwnProfileWarning"] =
                sent
                    ? "A new verification email was sent. "
                        + "Previous request links are no longer valid."
                    : "The request was renewed, but the email "
                        + "could not be sent. Please try again later.";

            return RedirectToAction(nameof(MyProfile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOwnEmailChange(
            Guid requestId)
        {
            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var profile =
                    await GetOwnTutorProfileAsync(tracking: true);

                if (profile == null)
                    return NotFound();

                var user = profile.ApplicationUser;

                if (requestId == Guid.Empty
                    || user.TutorEmailChangeRequestId != requestId)
                {
                    TempData["OwnProfileWarning"] =
                        "The pending request has changed. "
                        + "Please reload the page.";

                    return RedirectToAction(nameof(MyProfile));
                }

                user.PendingTutorEmail = null;
                user.TutorEmailChangeRequestId = null;
                user.TutorEmailChangeRequestedAtUtc = null;
                user.TutorEmailChangeExpiresAtUtc = null;
                user.TutorEmailChangeRequestedByUserId = null;

                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    TempData["OwnProfileWarning"] =
                        "Unable to cancel the request. Please try again.";

                    return RedirectToAction(nameof(MyProfile));
                }

                await transaction.CommitAsync();

                TempData["OwnProfileSuccess"] =
                    "The email change request was cancelled. "
                    + "Your current email is unchanged.";
            }
            catch (Exception ex) when (
                ex is DbUpdateException
                || ex is System.Data.Common.DbException)
            {
                _profileLogger.LogWarning(
                    "Tutor self email cancellation failed for {UserId}. "
                    + "Error type: {ErrorType}.",
                    _userManager.GetUserId(User),
                    ex.GetType().Name);

                TempData["OwnProfileWarning"] =
                    "Unable to cancel the request. Please try again.";
            }

            return RedirectToAction(nameof(MyProfile));
        }

        private static bool IsValidOwnTutorEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim();

            return email.Length <= 254
                && new System.ComponentModel.DataAnnotations
                    .EmailAddressAttribute().IsValid(email)
                && System.Text.RegularExpressions.Regex.IsMatch(
                    email,
                    @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@"
                    + @"(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+"
                    + @"[A-Za-z]{2,63}$",
                    System.Text.RegularExpressions.RegexOptions
                        .CultureInvariant);
        }

        // Read-only outcome for one protected email change request.
        // This does not accept a caller-supplied user ID or return profile data.
        [AllowAnonymous]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("PublicFormPolicy")]
        public async Task<IActionResult> EmailChangeOutcome(string? statusToken)
        {
            Response.Headers["Referrer-Policy"] = "no-referrer";

            if (string.IsNullOrWhiteSpace(statusToken) || statusToken.Length > 8192)
                return BadRequest();

            string[]? watchData;
            try
            {
                watchData = System.Text.Json.JsonSerializer.Deserialize<string[]>(
                    _emailWatchProtector.Unprotect(statusToken));
            }
            catch (Exception ex) when (
                ex is System.Security.Cryptography.CryptographicException
                || ex is System.Text.Json.JsonException)
            {
                return BadRequest();
            }

            if (watchData == null || watchData.Length != 4
                || string.IsNullOrWhiteSpace(watchData[0])
                || watchData[0].Length > 450
                || !Guid.TryParse(watchData[1], out var requestId)
                || requestId == Guid.Empty
                || string.IsNullOrWhiteSpace(watchData[2])
                || watchData[2].Length > 256
                || !long.TryParse(watchData[3],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long expiresAtTicks))
            {
                return BadRequest();
            }

            if (expiresAtTicks <= DateTime.UtcNow.Ticks
                || expiresAtTicks > DateTime.MaxValue.Ticks)
                return StatusCode(StatusCodes.Status410Gone);

            string userId = watchData[0];
            var account = await _context.TutorProfiles
                .AsNoTracking()
                .Where(profile => profile.ApplicationUserId == userId)
                .Select(profile => new
                {
                    active = profile.IsActive && profile.ApplicationUser.IsActive,
                    profile.ApplicationUser.Email,
                    profile.ApplicationUser.EmailConfirmed,
                    requestId = profile.ApplicationUser.TutorEmailChangeRequestId,
                    expiresAt = profile.ApplicationUser.TutorEmailChangeExpiresAtUtc
                })
                .FirstOrDefaultAsync();

            if (account == null || !account.active)
                return Json(new { state = "unavailable" });

            if (account.requestId == requestId)
            {
                bool expired = !account.expiresAt.HasValue
                    || account.expiresAt.Value <= DateTime.UtcNow;
                return Json(new { state = expired ? "expired" : "pending" });
            }

            if (account.EmailConfirmed && string.Equals(
                _userManager.NormalizeEmail(account.Email),
                watchData[2], StringComparison.Ordinal))
            {
                return Json(new { state = "confirmed" });
            }

            return Json(new
            {
                state = account.requestId.HasValue ? "replaced" : "cancelled"
            });
        }


        private async Task<TutorProfile?> GetOwnTutorProfileAsync(bool tracking = false, bool includeTeachingSubjects = false)
        {
            string? userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId)) return null;

            IQueryable<TutorProfile> query = _context.TutorProfiles
                .Include(x => x.ApplicationUser)
                .Where(x => x.ApplicationUserId == userId
                    && x.IsActive
                    && x.ApplicationUser.IsActive
                    && !x.ApplicationUser.MustChangePassword);

            if (includeTeachingSubjects) query = query.Include(x => x.TeachingSubjects);
            if (!tracking) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync();
        }
    }
}
