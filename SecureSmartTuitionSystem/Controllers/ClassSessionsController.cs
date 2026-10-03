using SecureSmartTuitionSystem.Services.Enrollment;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ClassSessionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] PrimaryLevels =
        {
            "YEAR 1",
            "YEAR 2",
            "YEAR 3",
            "YEAR 4",
            "YEAR 5",
            "YEAR 6"
        };

        private static readonly string[] SecondaryLevels =
        {
            "FORM 1",
            "FORM 2",
            "FORM 3",
            "FORM 4",
            "FORM 5"
        };

        private static readonly string[] YearOneToThreeSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI"
        };

        private static readonly string[] YearFourToSixSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI",
            "SEJ"
        };

        private static readonly string[] FormOneToThreeSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI",
            "SEJ",
            "GEO",
            "RBT",
            "ASK"
        };

        private static readonly string[] FormFourToFiveSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "AM",
            "BIO",
            "CHE",
            "PHY",
            "SEJ",
            "ACC",
            "EKO",
            "PER"
        };

        private static readonly string[] SubjectDisplayOrder =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI",
            "SEJ",
            "GEO",
            "RBT",
            "ASK",
            "AM",
            "BIO",
            "CHE",
            "PHY",
            "ACC",
            "EKO",
            "PER"
        };

        public ClassSessionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            ClassSessionIndexViewModel model)
        {
            model.SubjectName =
                NormalizeOptionalFilter(model.SubjectName)?.ToUpperInvariant();

            model.AcademicLevel =
                NormalizeOptionalFilter(model.AcademicLevel)?.ToUpperInvariant();

            model.FeeType =
                NormalizeOptionalFilter(model.FeeType)?.ToLowerInvariant();

            model.TutorUserId =
                NormalizeOptionalFilter(model.TutorUserId);

            model.SortBy = NormalizeSortColumn(model.SortBy);
            model.SortDirection =
                string.Equals(
                    model.SortDirection,
                    "desc",
                    StringComparison.OrdinalIgnoreCase)
                    ? "desc"
                    : "asc";

            IQueryable<ClassSession> query =
                _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Include(session => session.TutorUser);

            if (!string.IsNullOrWhiteSpace(model.SubjectName))
            {
                query = query.Where(session =>
                    session.Subject.SubjectName == model.SubjectName);
            }

            if (model.EducationStage.HasValue)
            {
                query = query.Where(session =>
                    session.Subject.EducationStage ==
                    model.EducationStage.Value);
            }

            if (!string.IsNullOrWhiteSpace(model.AcademicLevel))
            {
                query = query.Where(session =>
                    session.Subject.AcademicLevel == model.AcademicLevel);
            }

            if (model.FeeType == "free")
            {
                query = query.Where(session =>
                    session.Subject.MonthlyFee == 0);
            }
            else if (model.FeeType == "paid")
            {
                query = query.Where(session =>
                    session.Subject.MonthlyFee > 0);
            }

            if (!string.IsNullOrWhiteSpace(model.TutorUserId))
            {
                query = query.Where(session =>
                    session.TutorUserId == model.TutorUserId);
            }

            if (model.DayOfWeek.HasValue)
            {
                query = query.Where(session =>
                    session.DayOfWeek == model.DayOfWeek.Value);
            }

            if (model.Status.HasValue)
            {
                query = query.Where(session =>
                    session.Status == model.Status.Value);
            }

            query = ApplySorting(
                query,
                model.SortBy,
                model.SortDirection);

            model.Sessions = await query.ToListAsync();

            var classSessionIds =
                model.Sessions
                    .Select(session => session.ClassSessionId)
                    .ToList();

            model.EnrollmentCounts =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment =>
                        classSessionIds.Contains(
                            enrollment.ClassSessionId) &&
                        (
                            enrollment.Status ==
                                StudentEnrollmentStatus.Active ||

                            (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date))
                        ))
                    .GroupBy(enrollment =>
                        enrollment.ClassSessionId)
                    .Select(group => new
                    {
                        ClassSessionId = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.ClassSessionId,
                        item => item.Count);

            await PopulateIndexSelectionsAsync(model);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var session =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(item => item.Subject)
                    .Include(item => item.TutorUser)
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            int enrolledCount =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .CountAsync(enrollment =>
                        enrollment.ClassSessionId ==
                            session.ClassSessionId &&
                        (
                            enrollment.Status ==
                                StudentEnrollmentStatus.Active ||

                            (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date))
                        ));

            ViewBag.EnrolledCount = enrolledCount;

            return View(session);

        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ClassSessionFormViewModel
            {
                StartDate = ClassScheduleService.Today,
                Capacity = 20,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(20, 0)
            };

            await PopulateSelectionsAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ClassSessionFormViewModel model)
        {
            model.ClassSessionId = null; // Creation never trusts a posted existing class identifier.
            NormalizeInput(model);
            await GenerateClassNameIfEmptyAsync(model);

            await ValidateClassSessionAsync(
                model,
                existingClassSessionId: null);

            if (!ModelState.IsValid)
            {
                await PopulateSelectionsAsync(model);
                return View(model);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                await ValidateClassSessionAsync(model, existingClassSessionId: null);
                if (!ModelState.IsValid) { await PopulateSelectionsAsync(model); return View(model); }
                Subject subject =
                    await FindOrCreateSubjectAsync(model);

                string classCode =
                    await GenerateNextClassCodeAsync();

                var classSession = new ClassSession
                {
                    StartDate = model.StartDate!.Value.Date,
                    EndDate = model.EndDate?.Date,
                    ClassCode = classCode,
                    ClassName = model.ClassName!,
                    Subject = subject,
                    TutorUserId = model.TutorUserId,
                    DayOfWeek = model.DayOfWeek!.Value,
                    StartTime = model.StartTime!.Value,
                    EndTime = model.EndTime!.Value,
                    Venue = model.Venue,
                    Capacity = model.Capacity,
                    Remark = model.Remark,
                    Status = ClassSessionStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ClassSessions.Add(classSession);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Class {classCode} was created successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "The class session could not be saved. " +
                    "The class name may already exist.");

                await PopulateSelectionsAsync(model);

                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var session =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(item => item.Subject)
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            var model = new ClassSessionFormViewModel
            {
                ClassSessionId = session.ClassSessionId,
                StartDate = ClassScheduleService.FirstDate(session),
                EndDate = session.EndDate,
                EducationStage = session.Subject.EducationStage,
                AcademicLevel = session.Subject.AcademicLevel,
                SubjectName = session.Subject.SubjectName,
                MonthlyFee = session.Subject.MonthlyFee,
                ClassName = session.ClassName,
                TutorUserId = session.TutorUserId,
                DayOfWeek = session.DayOfWeek,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Venue = session.Venue,
                Capacity = session.Capacity,
                Remark = session.Remark
            };

            await PopulateSelectionsAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ClassSessionFormViewModel model)
        {
            if (model.ClassSessionId != id)
            {
                return BadRequest();
            }

            NormalizeInput(model);
            await GenerateClassNameIfEmptyAsync(model);

            await ValidateClassSessionAsync(
                model,
                existingClassSessionId: id);

            if (!ModelState.IsValid)
            {
                await PopulateSelectionsAsync(model);
                return View(model);
            }

            var session =
                await _context.ClassSessions
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                await ValidateClassSessionAsync(model, model.ClassSessionId);
                if (!ModelState.IsValid) { await PopulateSelectionsAsync(model); return View(model); }
                Subject subject =
                    await FindOrCreateSubjectAsync(model);

                session.StartDate = model.StartDate!.Value.Date;
                session.EndDate = model.EndDate?.Date;
                session.Subject = subject;
                session.ClassName = model.ClassName!;
                session.TutorUserId = model.TutorUserId;
                session.DayOfWeek = model.DayOfWeek!.Value;
                session.StartTime = model.StartTime!.Value;
                session.EndTime = model.EndTime!.Value;
                session.Venue = model.Venue;
                session.Capacity = model.Capacity;
                session.Remark = model.Remark;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Class {session.ClassName} " +
                    "was updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "The class session could not be saved. " +
                    "The Class Name may already exist.");

                await PopulateSelectionsAsync(model);

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (!await _context.ClassSessions.AnyAsync(x => x.ClassSessionId == id)) return NotFound();
            return RedirectToAction("Index", "ClassSchedules", new { classSessionId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var session =
                await _context.ClassSessions
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            if (session.Status != ClassSessionStatus.Active)
            {
                TempData["ErrorMessage"] =
                    "Only an active class can be marked as completed.";

                return RedirectToAction(nameof(Index));
            }

            if (!session.EndDate.HasValue || session.EndDate.Value.Date >= ClassScheduleService.Today ||
                await _context.ClassLessonChanges.AnyAsync(x => x.ClassSessionId == id && (x.ReplacementDate == null || x.ReplacementDate >= ClassScheduleService.Today)))
            {
                TempData["ErrorMessage"] = "Set a past term end date and resolve outstanding replacements before completing the class.";
                return RedirectToAction(nameof(Index));
            }
            if (await _context.EnrollmentApplications.AnyAsync(x => (x.Status == EnrollmentStatus.ApprovedPendingPayment || x.Status == EnrollmentStatus.PaymentConfirmed) && x.SelectedSubjects.Any(y => y.ClassSessionId == id)))
            { TempData["ErrorMessage"] = "Resolve approved applications awaiting payment before completing this class."; return RedirectToAction(nameof(Index)); }
            session.Status = ClassSessionStatus.Completed;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Class {session.ClassName} was marked as completed.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var session =
                await _context.ClassSessions
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            if (session.Status == ClassSessionStatus.Active)
            {
                TempData["ErrorMessage"] =
                    "This class is already active.";

                return RedirectToAction(nameof(Index));
            }

            bool conflict =
                await _context.ClassSessions.AnyAsync(item =>
                    item.ClassSessionId != session.ClassSessionId &&
                    item.TutorUserId == session.TutorUserId &&
                    item.DayOfWeek == session.DayOfWeek &&
                    item.Status == ClassSessionStatus.Active &&
                    (!session.EndDate.HasValue || (item.StartDate ?? item.CreatedAt.Date) <= session.EndDate.Value) &&
                    (!item.EndDate.HasValue || (session.StartDate ?? session.CreatedAt.Date) <= item.EndDate.Value) &&
                    session.StartTime < item.EndTime &&
                    session.EndTime > item.StartTime);

            if (conflict)
            {
                TempData["ErrorMessage"] =
                    "This class cannot be activated because the Tutor " +
                    "already has another active class during this time.";

                return RedirectToAction(nameof(Index));
            }

            if (session.EndDate.HasValue && session.EndDate.Value.Date < ClassScheduleService.Today)
            {
                TempData["ErrorMessage"] = "Extend the class end date before reactivating an ended term.";
                return RedirectToAction(nameof(Index));
            }
            session.Status = ClassSessionStatus.Active;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Class {session.ClassName} was activated.";

            return RedirectToAction(nameof(Index));
        }
        private static void NormalizeInput(
            ClassSessionFormViewModel model)
        {
            model.AcademicLevel =
                NormalizeText(model.AcademicLevel)
                    .ToUpperInvariant();

            model.SubjectName =
                NormalizeText(model.SubjectName)
                    .ToUpperInvariant();

            model.TutorUserId =
                NormalizeText(model.TutorUserId);

            model.Venue =
                NormalizeText(model.Venue);

            model.ClassName =
                string.IsNullOrWhiteSpace(model.ClassName)
                    ? null
                    : NormalizeText(model.ClassName);

            model.Remark =
                string.IsNullOrWhiteSpace(model.Remark)
                    ? null
                    : model.Remark.Trim();
        }

        private static string NormalizeText(string? value)
        {
            return Regex.Replace(
                value?.Trim() ?? string.Empty,
                @"\s+",
                " ");
        }

        private async Task GenerateClassNameIfEmptyAsync(
            ClassSessionFormViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.ClassName) ||
                string.IsNullOrWhiteSpace(model.AcademicLevel) ||
                string.IsNullOrWhiteSpace(model.SubjectName) ||
                string.IsNullOrWhiteSpace(model.TutorUserId) ||
                !model.DayOfWeek.HasValue ||
                !model.StartTime.HasValue)
            {
                return;
            }

            var tutor =
                await _userManager.FindByIdAsync(
                    model.TutorUserId);

            if (tutor == null)
            {
                return;
            }

            string tutorName =
                tutor.FullName ??
                tutor.Email ??
                "Tutor";

            string startTime =
                DateTime.Today
                    .Add(model.StartTime.Value.ToTimeSpan())
                    .ToString(
                        "hh:mm tt",
                        CultureInfo.InvariantCulture);

            model.ClassName =
                $"{model.AcademicLevel} - " +
                $"{model.SubjectName} - " +
                $"{tutorName} - " +
                $"{model.DayOfWeek.Value} - " +
                $"{startTime}";
        }

        private async Task ValidateClassSessionAsync(
            ClassSessionFormViewModel model,
            int? existingClassSessionId)
        {
            if (model.StartDate.HasValue)
            {
                model.EndDate ??= AcademicYearPolicy.EndOfYear(model.StartDate.Value);
                if (model.EndDate.Value.Year != model.StartDate.Value.Year) ModelState.AddModelError(nameof(model.EndDate), "A class term must end within its academic year (by 31 December).");
                if (model.StartDate.Value.Year < 2000 || model.StartDate.Value.Year > 2100)
                    ModelState.AddModelError(nameof(model.StartDate), "Choose a date between 2000 and 2100.");
                if (!existingClassSessionId.HasValue && model.StartDate.Value.Date < ClassScheduleService.Today)
                    ModelState.AddModelError(nameof(model.StartDate), "A new class must start today or later.");
                if (model.EndDate.HasValue && (model.EndDate.Value.Date < model.StartDate.Value.Date || model.EndDate.Value.Year > 2100))
                    ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after start date (through 2100).");
                if (model.EndDate.HasValue && model.DayOfWeek.HasValue &&
                    model.StartDate.Value.Date.AddDays(((int)model.DayOfWeek.Value - (int)model.StartDate.Value.DayOfWeek + 7) % 7) > model.EndDate.Value.Date)
                    ModelState.AddModelError(nameof(model.EndDate), "The date range must contain at least one lesson on the selected weekday.");
            }
            if (existingClassSessionId.HasValue)
            {
                var previous = await _context.ClassSessions.AsNoTracking().Include(x => x.Subject).FirstOrDefaultAsync(x => x.ClassSessionId == existingClassSessionId.Value);
                bool promised = await _context.EnrollmentApplications.AnyAsync(x => (x.Status == EnrollmentStatus.ApprovedPendingPayment || x.Status == EnrollmentStatus.PaymentConfirmed) && x.SelectedSubjects.Any(y => y.ClassSessionId == existingClassSessionId.Value)) ||
                    await _context.StudentEnrollments.AnyAsync(x => x.ClassSessionId == existingClassSessionId.Value && (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop));
                if (previous != null && (ClassScheduleService.FirstDate(previous) <= ClassScheduleService.Today || promised) &&
                    (model.StartDate?.Date != ClassScheduleService.FirstDate(previous) || model.DayOfWeek != previous.DayOfWeek ||
                     model.StartTime != previous.StartTime || model.EndTime != previous.EndTime || model.TutorUserId != previous.TutorUserId || model.SubjectName != previous.Subject.SubjectName || model.AcademicLevel != previous.Subject.AcademicLevel || model.EducationStage != previous.Subject.EducationStage))
                    ModelState.AddModelError("", "This class has started or already has approved/enrolled students. Its subject, start date, tutor and weekly timetable are preserved. Use Calendar to cancel a dated lesson and schedule its replacement.");
                if (model.EndDate.HasValue && await _context.StudentEnrollments.AnyAsync(x => x.ClassSessionId == existingClassSessionId.Value && x.StartDate > model.EndDate.Value &&
                    (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop)))
                    ModelState.AddModelError(nameof(model.EndDate), "An enrolled student starts after the proposed end date.");
                if (model.EndDate.HasValue)
                {
                    // Approved fee snapshots promise whole billing months; do not silently shorten that promise.
                    var commitments = await _context.EnrollmentApplications.AsNoTracking()
                        .Where(x => (x.Status == EnrollmentStatus.ApprovedPendingPayment || x.Status == EnrollmentStatus.PaymentConfirmed || x.Status == EnrollmentStatus.Enrolled) &&
                            x.SelectedSubjects.Any(y => y.ClassSessionId == existingClassSessionId.Value))
                        .Select(x => new { x.RequestedStartDate, x.EnrollmentDurationMonths }).ToListAsync();
                    if (commitments.Any(x => new DateTime(x.RequestedStartDate.Year, x.RequestedStartDate.Month, 1)
                        .AddMonths(x.EnrollmentDurationMonths).AddDays(-1) > model.EndDate.Value.Date))
                        ModelState.AddModelError(nameof(model.EndDate), "End date cannot be earlier than an approved or paid tuition period. Use a new class session for a shorter term.");
                }
                if (model.EndDate.HasValue && await _context.ClassLessonChanges.AnyAsync(x => x.ClassSessionId == existingClassSessionId.Value && x.OriginalDate > model.EndDate.Value))
                    ModelState.AddModelError(nameof(model.EndDate), "Do not shorten the term past an existing cancelled lesson. Replacement lessons may occur after the term.");
            }
            if (existingClassSessionId.HasValue && await new EnrollmentRulesService(_context).OccupiedSeatsAsync(existingClassSessionId.Value) > model.Capacity)
                ModelState.AddModelError(nameof(model.Capacity), "Maximum capacity cannot be below existing enrollments and approved reservations.");
            if (existingClassSessionId.HasValue && model.EndDate.HasValue && await _context.EnrollmentApplications.AnyAsync(x => (x.Status == EnrollmentStatus.ApprovedPendingPayment || x.Status == EnrollmentStatus.PaymentConfirmed) && x.RequestedStartDate > model.EndDate && x.SelectedSubjects.Any(y => y.ClassSessionId == existingClassSessionId)))
                ModelState.AddModelError(nameof(model.EndDate), "An approved application starts after the proposed end date.");
            if (existingClassSessionId.HasValue && model.StartDate.HasValue && model.DayOfWeek.HasValue && model.StartTime.HasValue && model.EndTime.HasValue)
            {
                var own = await _context.StudentEnrollments.AsNoTracking().Where(x => x.ClassSessionId == existingClassSessionId.Value &&
                    (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop)).ToListAsync();
                var ids = own.Select(x => x.StudentProfileId).ToList();
                var other = await _context.StudentEnrollments.AsNoTracking().Include(x => x.ClassSession).Where(x => ids.Contains(x.StudentProfileId) &&
                    x.ClassSessionId != existingClassSessionId.Value && x.ClassSession.Status == ClassSessionStatus.Active &&
                    (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop)).ToListAsync();
                foreach (var e in own)
                {
                    var begin = model.StartDate.Value > e.StartDate ? model.StartDate.Value : e.StartDate;
                    DateTime? end = model.EndDate;
                    if (e.DropEffectiveDate.HasValue && (!end.HasValue || e.DropEffectiveDate.Value <= end.Value)) end = e.DropEffectiveDate.Value.AddDays(-1);
                    foreach (var o in other.Where(x => x.StudentProfileId == e.StudentProfileId))
                    {
                        var otherBegin = ClassScheduleService.FirstDate(o.ClassSession) > o.StartDate ? ClassScheduleService.FirstDate(o.ClassSession) : o.StartDate;
                        DateTime? otherEnd = AcademicYearPolicy.ClassEnd(o.ClassSession);
                        if (o.DropEffectiveDate.HasValue && (!otherEnd.HasValue || o.DropEffectiveDate.Value <= otherEnd.Value)) otherEnd = o.DropEffectiveDate.Value.AddDays(-1);
                        if (ClassScheduleService.WeeklyOverlap(begin, end, model.DayOfWeek.Value, model.StartTime.Value, model.EndTime.Value,
                            otherBegin, otherEnd, o.ClassSession.DayOfWeek, o.ClassSession.StartTime, o.ClassSession.EndTime))
                            ModelState.AddModelError("", "The proposed timetable conflicts with another class of an enrolled student.");
                    }
                }
            }
            if (existingClassSessionId.HasValue && model.EndDate.HasValue)
            {
                var commitments = await _context.EnrollmentApplications.AsNoTracking().Where(a =>
                    (a.Status == EnrollmentStatus.ApprovedPendingPayment || a.Status == EnrollmentStatus.PaymentConfirmed || a.Status == EnrollmentStatus.Enrolled) &&
                    a.SelectedSubjects.Any(x => x.ClassSessionId == existingClassSessionId.Value)).Select(a => new { a.RequestedStartDate, a.EnrollmentDurationMonths }).ToListAsync();
                if (commitments.Any(a => new DateTime(a.RequestedStartDate.Year, a.RequestedStartDate.Month, 1).AddMonths(a.EnrollmentDurationMonths).AddDays(-1) > model.EndDate.Value.Date))
                    ModelState.AddModelError(nameof(model.EndDate), "The proposed end date would shorten an approved or paid tuition period. Review those commitments first.");
            }
            ValidateStageAndLevel(model);
            ValidateSubject(model);
            ValidateTime(model);
            ValidateGoogleMeetLink(model);

            if (!string.IsNullOrWhiteSpace(
                    model.TutorUserId))
            {
                var tutor =
                    await _userManager.FindByIdAsync(
                        model.TutorUserId);

                bool validTutor =
                    tutor != null &&
                    tutor.IsActive &&
                    await _userManager.IsInRoleAsync(
                        tutor,
                        "Tutor");

                if (!validTutor)
                {
                    ModelState.AddModelError(
                        nameof(model.TutorUserId),
                        "The selected Tutor is not valid.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.ClassName))
            {
                ModelState.AddModelError(
                    nameof(model.ClassName),
                    "Class Name is required.");
            }
            else
            {
                bool duplicateName =
                    await _context.ClassSessions.AnyAsync(
                        session =>
                            session.ClassName ==
                                model.ClassName &&
                            (!existingClassSessionId.HasValue ||
                             session.ClassSessionId !=
                                existingClassSessionId.Value));

                if (duplicateName)
                {
                    ModelState.AddModelError(
                        nameof(model.ClassName),
                        "This Class Name already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    model.TutorUserId) &&
                model.DayOfWeek.HasValue &&
                model.StartTime.HasValue &&
                model.EndTime.HasValue &&
                model.StartTime.Value <
                    model.EndTime.Value)
            {
                var candidates = await _context.ClassSessions.AsNoTracking().Where(session =>
                    session.TutorUserId == model.TutorUserId && session.DayOfWeek == model.DayOfWeek.Value &&
                    session.Status == ClassSessionStatus.Active && (!existingClassSessionId.HasValue || session.ClassSessionId != existingClassSessionId.Value) &&
                    model.StartTime.Value < session.EndTime && model.EndTime.Value > session.StartTime).ToListAsync();
                bool conflict = model.StartDate.HasValue && candidates.Any(session => ClassScheduleService.WeeklyOverlap(
                    model.StartDate.Value, model.EndDate, model.DayOfWeek.Value, model.StartTime.Value, model.EndTime.Value,
                    ClassScheduleService.FirstDate(session), AcademicYearPolicy.ClassEnd(session), session.DayOfWeek, session.StartTime, session.EndTime));
                if (!conflict && model.StartDate.HasValue)
                {
                    var replacements = await _context.ClassLessonChanges.AsNoTracking().Where(x => x.ReplacementDate != null &&
                        x.ClassSession.TutorUserId == model.TutorUserId && x.ReplacementDate >= model.StartDate.Value.Date &&
                        (!model.EndDate.HasValue || x.ReplacementDate <= model.EndDate.Value.Date) &&
                        x.ReplacementStartTime < model.EndTime.Value && x.ReplacementEndTime > model.StartTime.Value).ToListAsync();
                    conflict = replacements.Any(x => x.ReplacementDate!.Value.DayOfWeek == model.DayOfWeek.Value);
                }
                if (conflict)
                {
                    ModelState.AddModelError(
                        nameof(model.StartTime),
                        "The Tutor already has another class " +
                        "during this time.");
                }
            }
        }

        private void ValidateStageAndLevel(
            ClassSessionFormViewModel model)
        {
            if (!model.EducationStage.HasValue ||
                string.IsNullOrWhiteSpace(
                    model.AcademicLevel))
            {
                return;
            }

            bool validLevel =
                model.EducationStage ==
                    Models.Enrollment.EducationStage.Primary
                    ? PrimaryLevels.Contains(
                        model.AcademicLevel)
                    : SecondaryLevels.Contains(
                        model.AcademicLevel);

            if (!validLevel)
            {
                ModelState.AddModelError(
                    nameof(model.AcademicLevel),
                    "The Academic Level does not match " +
                    "the selected Education Stage.");
            }
        }

        private void ValidateSubject(
            ClassSessionFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(
                    model.AcademicLevel) ||
                string.IsNullOrWhiteSpace(
                    model.SubjectName))
            {
                return;
            }

            string[] allowedSubjects =
                GetSubjectsForLevel(
                    model.AcademicLevel);

            if (!allowedSubjects.Contains(
                    model.SubjectName))
            {
                ModelState.AddModelError(
                    nameof(model.SubjectName),
                    "The selected Subject is not available " +
                    "for this Academic Level.");
            }
        }

        private void ValidateTime(
            ClassSessionFormViewModel model)
        {
            if (!model.StartTime.HasValue ||
                !model.EndTime.HasValue)
            {
                return;
            }

            if (model.StartTime.Value >=
                model.EndTime.Value)
            {
                ModelState.AddModelError(
                    nameof(model.StartTime),
                    "Start Time must be earlier than End Time.");

                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "End Time must be later than Start Time.");

                return;
            }

            TimeSpan duration =
                model.EndTime.Value -
                model.StartTime.Value;

            if (duration < TimeSpan.FromMinutes(30))
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Class duration must be at least 30 minutes.");
            }

            if (duration > TimeSpan.FromHours(4))
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Class duration cannot exceed 4 hours.");
            }
        }

        private void ValidateGoogleMeetLink(
            ClassSessionFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Venue))
            {
                return;
            }

            bool valid =
                Uri.TryCreate(
                    model.Venue,
                    UriKind.Absolute,
                    out Uri? uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                string.Equals(
                    uri.Host,
                    "meet.google.com",
                    StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.Trim('/').Length > 0;

            if (!valid)
            {
                ModelState.AddModelError(
                    nameof(model.Venue),
                    "Please enter a valid Google Meet link, for example: " +
                    "https://meet.google.com/abc-defg-hij");
            }
        }

        private static string[] GetSubjectsForLevel(
            string academicLevel)
        {
            return academicLevel switch
            {
                "YEAR 1" or "YEAR 2" or "YEAR 3"
                    => YearOneToThreeSubjects,

                "YEAR 4" or "YEAR 5" or "YEAR 6"
                    => YearFourToSixSubjects,

                "FORM 1" or "FORM 2" or "FORM 3"
                    => FormOneToThreeSubjects,

                "FORM 4" or "FORM 5"
                    => FormFourToFiveSubjects,

                _ => Array.Empty<string>()
            };
        }

        private async Task<Subject> FindOrCreateSubjectAsync(
            ClassSessionFormViewModel model)
        {
            var subject =
                await _context.Subjects
                    .FirstOrDefaultAsync(item =>
                        item.EducationStage ==
                            model.EducationStage!.Value &&
                        item.AcademicLevel ==
                            model.AcademicLevel &&
                        item.SubjectName ==
                            model.SubjectName);

            if (subject != null)
            {
                subject.MonthlyFee =
                    model.MonthlyFee;

                subject.IsActive = true;

                return subject;
            }

            subject = new Subject
            {
                EducationStage =
                    model.EducationStage!.Value,

                AcademicLevel =
                    model.AcademicLevel,

                SubjectName =
                    model.SubjectName,

                MonthlyFee =
                    model.MonthlyFee,

                IsActive = true
            };

            _context.Subjects.Add(subject);

            return subject;
        }

        private async Task<string> GenerateNextClassCodeAsync()
        {
            string? latestCode =
                await _context.ClassSessions
                    .Where(session =>
                        session.ClassCode.StartsWith("CS") &&
                        session.ClassCode.Length == 7)
                    .OrderByDescending(session =>
                        session.ClassCode)
                    .Select(session =>
                        session.ClassCode)
                    .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrWhiteSpace(latestCode) &&
                int.TryParse(
                    latestCode.Substring(2),
                    out int currentNumber))
            {
                nextNumber = currentNumber + 1;
            }

            if (nextNumber > 99999)
            {
                throw new InvalidOperationException(
                    "The maximum Class Code has been reached.");
            }

            return $"CS{nextNumber:D5}";
        }

        private static string? NormalizeOptionalFilter(
            string? value)
        {
            string normalized = NormalizeText(value);

            return string.IsNullOrWhiteSpace(normalized)
                ? null
                : normalized;
        }

        private static string NormalizeSortColumn(
            string? sortBy)
        {
            return sortBy?.Trim().ToLowerInvariant() switch
            {
                "subject" => "subject",
                "educationstage" => "educationStage",
                "academiclevel" => "academicLevel",
                "monthlyfee" => "monthlyFee",
                "tutor" => "tutor",
                "schedule" => "schedule",
                "capacity" => "capacity",
                "status" => "status",
                _ => "className"
            };
        }

        private static IQueryable<ClassSession> ApplySorting(
            IQueryable<ClassSession> query,
            string sortBy,
            string sortDirection)
        {
            bool descending = sortDirection == "desc";

            return sortBy switch
            {
                "subject" => descending
                    ? query
                        .OrderByDescending(session =>
                            session.Subject.SubjectName)
                        .ThenByDescending(session =>
                            session.Subject.AcademicLevel)
                    : query
                        .OrderBy(session =>
                            session.Subject.SubjectName)
                        .ThenBy(session =>
                            session.Subject.AcademicLevel),

                "educationStage" => descending
                    ? query
                        .OrderByDescending(session =>
                            session.Subject.EducationStage)
                        .ThenByDescending(session =>
                            session.Subject.AcademicLevel)
                    : query
                        .OrderBy(session =>
                            session.Subject.EducationStage)
                        .ThenBy(session =>
                            session.Subject.AcademicLevel),

                "academicLevel" => descending
                    ? query.OrderByDescending(session =>
                        session.Subject.AcademicLevel)
                    : query.OrderBy(session =>
                        session.Subject.AcademicLevel),

                "monthlyFee" => descending
                    ? query.OrderByDescending(session =>
                        session.Subject.MonthlyFee)
                    : query.OrderBy(session =>
                        session.Subject.MonthlyFee),

                "tutor" => descending
                    ? query.OrderByDescending(session =>
                        session.TutorUser.FullName ??
                        session.TutorUser.Email)
                    : query.OrderBy(session =>
                        session.TutorUser.FullName ??
                        session.TutorUser.Email),

                "schedule" => descending
                    ? query
                        .OrderByDescending(session =>
                            session.DayOfWeek == DayOfWeek.Monday ? 1 :
                            session.DayOfWeek == DayOfWeek.Tuesday ? 2 :
                            session.DayOfWeek == DayOfWeek.Wednesday ? 3 :
                            session.DayOfWeek == DayOfWeek.Thursday ? 4 :
                            session.DayOfWeek == DayOfWeek.Friday ? 5 :
                            session.DayOfWeek == DayOfWeek.Saturday ? 6 :
                            session.DayOfWeek == DayOfWeek.Sunday ? 7 : 8)
                        .ThenByDescending(session =>
                            session.StartTime)
                    : query
                        .OrderBy(session =>
                            session.DayOfWeek == DayOfWeek.Monday ? 1 :
                            session.DayOfWeek == DayOfWeek.Tuesday ? 2 :
                            session.DayOfWeek == DayOfWeek.Wednesday ? 3 :
                            session.DayOfWeek == DayOfWeek.Thursday ? 4 :
                            session.DayOfWeek == DayOfWeek.Friday ? 5 :
                            session.DayOfWeek == DayOfWeek.Saturday ? 6 :
                            session.DayOfWeek == DayOfWeek.Sunday ? 7 : 8)
                        .ThenBy(session =>
                            session.StartTime),

                "capacity" => descending
                    ? query.OrderByDescending(session =>
                        session.Capacity)
                    : query.OrderBy(session =>
                        session.Capacity),

                "status" => descending
                    ? query.OrderByDescending(session =>
                        session.Status)
                    : query.OrderBy(session =>
                        session.Status),

                _ => ApplyClassNameSorting(
                    query,
                    descending)
            };
        }

        private static IQueryable<ClassSession>
            ApplyClassNameSorting(
                IQueryable<ClassSession> query,
                bool descending)
        {
            if (descending)
            {
                return query
                    .OrderByDescending(session =>
                        session.Subject.AcademicLevel == "YEAR 1" ? 1 :
                        session.Subject.AcademicLevel == "YEAR 2" ? 2 :
                        session.Subject.AcademicLevel == "YEAR 3" ? 3 :
                        session.Subject.AcademicLevel == "YEAR 4" ? 4 :
                        session.Subject.AcademicLevel == "YEAR 5" ? 5 :
                        session.Subject.AcademicLevel == "YEAR 6" ? 6 :
                        session.Subject.AcademicLevel == "FORM 1" ? 7 :
                        session.Subject.AcademicLevel == "FORM 2" ? 8 :
                        session.Subject.AcademicLevel == "FORM 3" ? 9 :
                        session.Subject.AcademicLevel == "FORM 4" ? 10 :
                        session.Subject.AcademicLevel == "FORM 5" ? 11 :
                        99)

                    .ThenByDescending(session =>
                        session.Subject.SubjectName == "BC" ? 1 :
                        session.Subject.SubjectName == "BMP" ? 2 :
                        session.Subject.SubjectName == "BMK" ? 3 :
                        session.Subject.SubjectName == "BIP" ? 4 :
                        session.Subject.SubjectName == "BIK" ? 5 :
                        session.Subject.SubjectName == "MM" ? 6 :
                        session.Subject.SubjectName == "SCI" ? 7 :
                        session.Subject.SubjectName == "SEJ" ? 8 :
                        session.Subject.SubjectName == "GEO" ? 9 :
                        session.Subject.SubjectName == "RBT" ? 10 :
                        session.Subject.SubjectName == "ASK" ? 11 :
                        session.Subject.SubjectName == "AM" ? 12 :
                        session.Subject.SubjectName == "BIO" ? 13 :
                        session.Subject.SubjectName == "CHE" ? 14 :
                        session.Subject.SubjectName == "PHY" ? 15 :
                        session.Subject.SubjectName == "ACC" ? 16 :
                        session.Subject.SubjectName == "EKO" ? 17 :
                        session.Subject.SubjectName == "PER" ? 18 :
                        99)

                    .ThenByDescending(session =>
                        session.TutorUser.FullName ??
                        session.TutorUser.Email)

                    .ThenByDescending(session =>
                        session.DayOfWeek == DayOfWeek.Monday ? 1 :
                        session.DayOfWeek == DayOfWeek.Tuesday ? 2 :
                        session.DayOfWeek == DayOfWeek.Wednesday ? 3 :
                        session.DayOfWeek == DayOfWeek.Thursday ? 4 :
                        session.DayOfWeek == DayOfWeek.Friday ? 5 :
                        session.DayOfWeek == DayOfWeek.Saturday ? 6 :
                        session.DayOfWeek == DayOfWeek.Sunday ? 7 :
                        8)

                    .ThenByDescending(session =>
                        session.StartTime);
            }

            return query
                .OrderBy(session =>
                    session.Subject.AcademicLevel == "YEAR 1" ? 1 :
                    session.Subject.AcademicLevel == "YEAR 2" ? 2 :
                    session.Subject.AcademicLevel == "YEAR 3" ? 3 :
                    session.Subject.AcademicLevel == "YEAR 4" ? 4 :
                    session.Subject.AcademicLevel == "YEAR 5" ? 5 :
                    session.Subject.AcademicLevel == "YEAR 6" ? 6 :
                    session.Subject.AcademicLevel == "FORM 1" ? 7 :
                    session.Subject.AcademicLevel == "FORM 2" ? 8 :
                    session.Subject.AcademicLevel == "FORM 3" ? 9 :
                    session.Subject.AcademicLevel == "FORM 4" ? 10 :
                    session.Subject.AcademicLevel == "FORM 5" ? 11 :
                    99)

                .ThenBy(session =>
                    session.Subject.SubjectName == "BC" ? 1 :
                    session.Subject.SubjectName == "BMP" ? 2 :
                    session.Subject.SubjectName == "BMK" ? 3 :
                    session.Subject.SubjectName == "BIP" ? 4 :
                    session.Subject.SubjectName == "BIK" ? 5 :
                    session.Subject.SubjectName == "MM" ? 6 :
                    session.Subject.SubjectName == "SCI" ? 7 :
                    session.Subject.SubjectName == "SEJ" ? 8 :
                    session.Subject.SubjectName == "GEO" ? 9 :
                    session.Subject.SubjectName == "RBT" ? 10 :
                    session.Subject.SubjectName == "ASK" ? 11 :
                    session.Subject.SubjectName == "AM" ? 12 :
                    session.Subject.SubjectName == "BIO" ? 13 :
                    session.Subject.SubjectName == "CHE" ? 14 :
                    session.Subject.SubjectName == "PHY" ? 15 :
                    session.Subject.SubjectName == "ACC" ? 16 :
                    session.Subject.SubjectName == "EKO" ? 17 :
                    session.Subject.SubjectName == "PER" ? 18 :
                    99)

                .ThenBy(session =>
                    session.TutorUser.FullName ??
                    session.TutorUser.Email)

                .ThenBy(session =>
                    session.DayOfWeek == DayOfWeek.Monday ? 1 :
                    session.DayOfWeek == DayOfWeek.Tuesday ? 2 :
                    session.DayOfWeek == DayOfWeek.Wednesday ? 3 :
                    session.DayOfWeek == DayOfWeek.Thursday ? 4 :
                    session.DayOfWeek == DayOfWeek.Friday ? 5 :
                    session.DayOfWeek == DayOfWeek.Saturday ? 6 :
                    session.DayOfWeek == DayOfWeek.Sunday ? 7 :
                    8)

                .ThenBy(session =>
                    session.StartTime);
        }

        private async Task PopulateIndexSelectionsAsync(
            ClassSessionIndexViewModel model)
        {
            var subjectQuery =
                _context.Subjects
                    .AsNoTracking()
                    .Where(subject => subject.IsActive);

            if (model.EducationStage.HasValue)
            {
                subjectQuery = subjectQuery.Where(subject =>
                    subject.EducationStage ==
                        model.EducationStage.Value);
            }

            /*
             * When an Academic Level is selected, the Subject
             * dropdown must contain only the subjects that are
             * valid for that exact level.
             */
            if (!string.IsNullOrWhiteSpace(model.AcademicLevel))
            {
                subjectQuery = subjectQuery.Where(subject =>
                    subject.AcademicLevel ==
                        model.AcademicLevel);
            }

            var subjectNames =
                await subjectQuery
                    .Select(subject => subject.SubjectName)
                    .Distinct()
                    .ToListAsync();

            model.Subjects =
                subjectNames
                    .OrderBy(name =>
                    {
                        int position = Array.IndexOf(
                            SubjectDisplayOrder,
                            name.ToUpperInvariant());

                        return position >= 0
                            ? position
                            : int.MaxValue;
                    })
                    .ThenBy(name => name)
                    .Select(name => new SelectListItem
                    {
                        Value = name,
                        Text = name,
                        Selected = string.Equals(
                            name,
                            model.SubjectName,
                            StringComparison.OrdinalIgnoreCase)
                    })
                    .ToList();

            IEnumerable<string> levels =
                model.EducationStage switch
                {
                    Models.Enrollment.EducationStage.Primary
                        => PrimaryLevels,

                    Models.Enrollment.EducationStage.Secondary
                        => SecondaryLevels,

                    _ => PrimaryLevels.Concat(SecondaryLevels)
                };

            model.AcademicLevels =
                levels.Select(level => new SelectListItem
                {
                    Value = level,
                    Text = level,
                    Selected = level == model.AcademicLevel
                }).ToList();

            var tutors =
                await _userManager.GetUsersInRoleAsync("Tutor");

            model.Tutors =
                tutors
                    .Where(tutor => tutor.IsActive)
                    .OrderBy(tutor =>
                        tutor.FullName ?? tutor.Email)
                    .Select(tutor => new SelectListItem
                    {
                        Value = tutor.Id,
                        Text = tutor.FullName ??
                            tutor.Email ??
                            "Tutor",
                        Selected = tutor.Id == model.TutorUserId
                    })
                    .ToList();
        }

        private async Task PopulateSelectionsAsync(
            ClassSessionFormViewModel model)
        {
            model.EducationStages = new()
            {
                new SelectListItem
                {
                    Value = "Primary",
                    Text = "Primary",
                    Selected =
                        model.EducationStage ==
                        Models.Enrollment.EducationStage.Primary
                },
                new SelectListItem
                {
                    Value = "Secondary",
                    Text = "Secondary",
                    Selected =
                        model.EducationStage ==
                        Models.Enrollment.EducationStage.Secondary
                }
            };

            var tutors =
                await _userManager.GetUsersInRoleAsync(
                    "Tutor");

            model.Tutors =
                tutors
                    .Where(tutor => tutor.IsActive)
                    .OrderBy(tutor =>
                        tutor.FullName ??
                        tutor.Email)
                    .Select(tutor =>
                        new SelectListItem
                        {
                            Value = tutor.Id,

                            Text =
                                tutor.FullName ??
                                tutor.Email ??
                                "Tutor",

                            Selected =
                                model.TutorUserId ==
                                tutor.Id
                        })
                    .ToList();

            model.Days =
                Enum.GetValues<DayOfWeek>()
                    .Select(day =>
                        new SelectListItem
                        {
                            Value = day.ToString(),
                            Text = day.ToString(),

                            Selected =
                                model.DayOfWeek == day
                        })
                    .ToList();
        }
    }
}