using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [AllowAnonymous]
    [EnableRateLimiting("PublicFormPolicy")]
    public class EnrollmentSubmissionController : Controller
    {
        private const string StudentDetailsSessionKey =
            "Enrollment.StudentDetails";

        private const string GuardianDetailsSessionKey =
            "Enrollment.GuardianDetails";

        private const string ExistingParentUserIdSessionKey =
            "Enrollment.ExistingParentUserId";

        private const string LastSubmittedApplicationIdSessionKey =
            "Enrollment.LastSubmittedApplicationId";

        private const string SelectedClassIdsSessionKey =
            "Enrollment.SelectedClassIds";

        private const string SelectClassesViewPath =
            "~/Views/Enrollment/SelectClasses.cshtml";

        private const string SubmittedViewPath =
            "~/Views/Enrollment/Submitted.cshtml";

        private const string EnrollmentOptionsViewPath =
            "~/Views/Enrollment/EnrollmentOptions.cshtml";

        private const decimal HardcopyFeePerSubject = 10.00m;

        private const decimal WestMalaysiaShippingFee = 10.00m;

        private const decimal EastMalaysiaShippingFee = 18.00m;

        private readonly EnrollmentNoticeService _notices;
        private readonly ApplicationDbContext _context;
        private readonly SecurityEventService _securityEvents;

        private readonly UserManager<ApplicationUser>
            _userManager;

        private readonly ILogger<EnrollmentSubmissionController>
            _logger;

        public EnrollmentSubmissionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<EnrollmentSubmissionController> logger, SecurityEventService securityEvents, EnrollmentNoticeService notices)
        {
            _context = context;
            _notices = notices;
            _userManager = userManager;
            _logger = logger;
            _securityEvents = securityEvents;
        }

        /*
         * =====================================================
         * STEP 3 - SELECT CLASSES
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> SelectClasses()
        {
            var studentDetails =
                GetStudentDetailsFromSession();

            if (studentDetails == null)
            {
                return ReturnToStudentDetails();
            }

            var guardianDetails =
                GetGuardianDetailsFromSession();

            if (guardianDetails == null)
            {
                return ReturnToGuardianDetails();
            }

            var model =
                new EnrollmentSelectionViewModel
                {
                    StudentName =
                        studentDetails.StudentName,

                    GuardianName =
                        GetGuardianDisplayName(
                            guardianDetails),

                    RequestedStartDate = GetRequestedStartDate(),

                    EnrollmentDurationMonths = 1
                };

            model.SelectedClassSessionIds =
                GetSelectedClassIdsFromSession();

            await PopulateSelectionViewModelAsync(
                model);

            return View(
                SelectClassesViewPath,
                model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ContinueToOptions(
            EnrollmentSelectionViewModel model)
        {
            var studentDetails = GetStudentDetailsFromSession();
            if (studentDetails == null) return ReturnToStudentDetails();
            var guardianDetails = GetGuardianDetailsFromSession();
            if (guardianDetails == null) return ReturnToGuardianDetails();

            // Display-only identity fields must be restored from the trusted session on POST.
            model.StudentName = studentDetails.StudentName;
            model.GuardianName = GetGuardianDisplayName(guardianDetails);
            ModelState.Remove(nameof(model.StudentName));
            ModelState.Remove(nameof(model.GuardianName));

            model.SelectedClassSessionIds =
                model.SelectedClassSessionIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

            if (model.SelectedClassSessionIds.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(model.SelectedClassSessionIds),
                    "Please select at least one class.");

                await PopulateSelectionViewModelAsync(model);
                return View(SelectClassesViewPath, model);
            }

            if (!model.RequestedStartDate.HasValue || model.RequestedStartDate.Value.Date < ClassScheduleService.Today || model.RequestedStartDate.Value.Date > ClassScheduleService.Today.AddYears(1))
            { ModelState.AddModelError(nameof(model.RequestedStartDate), "Choose a start date from today through the next year."); await PopulateSelectionViewModelAsync(model); return View(SelectClassesViewPath, model); }

            var selectedClasses =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        model.SelectedClassSessionIds.Contains(
                            session.ClassSessionId) &&
                        session.Status == ClassSessionStatus.Active &&
                        (!session.EndDate.HasValue || session.EndDate.Value >= DateTime.UtcNow.AddHours(8).Date) &&
                        (session.StartDate ?? session.CreatedAt.AddHours(8)).Year >= DateTime.UtcNow.AddHours(8).Year &&
                        session.Subject.IsActive)
                    .ToListAsync();

            if (selectedClasses.Count !=
                model.SelectedClassSessionIds.Count)
            {
                ModelState.AddModelError(
                    nameof(model.SelectedClassSessionIds),
                    "One or more selected classes are no longer available.");

                await PopulateSelectionViewModelAsync(model);
                return View(SelectClassesViewPath, model);
            }

            if (selectedClasses.Any(c => !AcademicYearPolicy.CanEnrollOn(c, model.RequestedStartDate!.Value)))
            { ModelState.AddModelError("", "Choose classes valid for the selected start date and academic year."); await PopulateSelectionViewModelAsync(model); return View(SelectClassesViewPath, model); }
            int? addStudentId = HttpContext.Session.GetInt32("Enrollment.AddSubjectStudentId");
            if (addStudentId.HasValue)
            {
                string? userId = _userManager.GetUserId(User);
                if (userId == null || !await _context.StudentProfiles.AnyAsync(x => x.StudentProfileId == addStudentId.Value && x.IsActive &&
                    (User.IsInRole("Admin") || (User.IsInRole("Student") && x.ApplicationUserId == userId) ||
                     (User.IsInRole("Parent") && x.ParentStudents.Any(link => link.IsActive && link.ParentProfile.ApplicationUserId == userId))))) return Forbid();
                var check = await EnrollmentAvailability.LoadAsync(_context,model.SelectedClassSessionIds.ToList(),addStudentId);
                if (selectedClasses.Any(x => check.SubjectBlocked(addStudentId.Value,x.SubjectId,model.RequestedStartDate!.Value,AcademicYearPolicy.ClassEnd(x))))
                {
                    ModelState.AddModelError("", "This subject overlaps an existing enrollment or pending application. Choose a start date after it ends.");
                    await PopulateSelectionViewModelAsync(model);
                    return View(SelectClassesViewPath, model);
                }
            }
            HttpContext.Session.SetString("Enrollment.RequestedStartDate", model.RequestedStartDate!.Value.ToString("yyyy-MM-dd"));

            SaveSelectedClassIdsToSession(
                model.SelectedClassSessionIds);

            return RedirectToAction(nameof(EnrollmentOptions));
        }

        [HttpGet]
        public async Task<IActionResult> EnrollmentOptions()
        {
            var studentDetails = GetStudentDetailsFromSession();
            if (studentDetails == null)
            {
                return ReturnToStudentDetails();
            }

            var guardianDetails = GetGuardianDetailsFromSession();
            if (guardianDetails == null)
            {
                return ReturnToGuardianDetails();
            }

            var selectedIds = GetSelectedClassIdsFromSession();
            if (selectedIds.Count == 0)
            {
                return RedirectToAction(nameof(SelectClasses));
            }

            var model = new EnrollmentSelectionViewModel
            {
                StudentName = studentDetails.StudentName,
                GuardianName = GetGuardianDisplayName(guardianDetails),
                SelectedClassSessionIds = selectedIds,
                RequestedStartDate = GetRequestedStartDate(),
                EnrollmentDurationMonths = 1
            };

            await PopulateSelectionViewModelAsync(model);
            SetFeePreview(model);

            return View(EnrollmentOptionsViewPath, model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BackToSelectClasses()
        {
            return RedirectToAction(nameof(SelectClasses));
        }

        /*
         * =====================================================
         * RETURN TO GUARDIAN DETAILS
         * =====================================================
         */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BackToGuardianDetails()
        {
            if (HttpContext.Session.GetInt32("Enrollment.AddSubjectStudentId").HasValue)
                return RedirectToAction(nameof(SelectClasses));
            return RedirectToAction(
                "GuardianDetails",
                "Enrollment");
        }

        /*
         * =====================================================
         * SUBMIT ENROLLMENT APPLICATION
         * =====================================================
         */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            EnrollmentSelectionViewModel model)
        {
            int? addStudentId = HttpContext.Session.GetInt32("Enrollment.AddSubjectStudentId");
            if (addStudentId.HasValue)
            {
                string? userId = _userManager.GetUserId(User);
                var lockedStudent = await _context.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.StudentProfileId == addStudentId && x.IsActive &&
                    (User.IsInRole("Admin") || (User.IsInRole("Student") && x.ApplicationUserId == userId) || (User.IsInRole("Parent") && x.ParentStudents.Any(link => link.IsActive && link.ParentProfile.ApplicationUserId == userId))));
                if (lockedStudent == null || GetStudentDetailsFromSession()?.StudentIdentificationNumber != lockedStudent.IdentificationNumber) return Forbid();
                string? guardianId = HttpContext.Session.GetString(ExistingParentUserIdSessionKey);
                if ((User.IsInRole("Parent") && guardianId != userId) || !await _context.ParentStudents.AnyAsync(x => x.IsActive && x.StudentProfileId == addStudentId && x.ParentProfile.ApplicationUserId == guardianId)) return Forbid();
            }
            var studentDetails =
                GetStudentDetailsFromSession();

            if (studentDetails == null)
            {
                return ReturnToStudentDetails();
            }

            var guardianDetails =
                GetGuardianDetailsFromSession();

            if (guardianDetails == null)
            {
                return ReturnToGuardianDetails();
            }

            var enrollmentVerification = HttpContext.RequestServices.GetRequiredService<GuardianEnrollmentVerification>();
            var consentParent = !addStudentId.HasValue && guardianDetails.UseExistingParentAccount
                ? await enrollmentVerification.GetVerifiedAsync(HttpContext) : null;
            if (!addStudentId.HasValue && guardianDetails.UseExistingParentAccount &&
                consentParent == null && (!User.IsInRole("Parent") || _userManager.GetUserId(User) !=
                    HttpContext.Session.GetString(ExistingParentUserIdSessionKey)))
                return ReturnToGuardianDetails();

            model.StudentName =
                studentDetails.StudentName;

            model.GuardianName =
                GetGuardianDisplayName(
                    guardianDetails);

            ModelState.Remove(nameof(model.StudentName));
            ModelState.Remove(nameof(model.GuardianName));
            if (model.SelectedClassSessionIds == null ||
                model.SelectedClassSessionIds.Count == 0)
            {
                model.SelectedClassSessionIds =
                    GetSelectedClassIdsFromSession();
            }

            model.SelectedClassSessionIds =
                model.SelectedClassSessionIds
                    .Where(classId => classId > 0)
                    .Distinct()
                    .ToList();

            NormalizeDeliveryFields(model);

            if (!ModelState.IsValid)
            {
                await PopulateSelectionViewModelAsync(
                    model);

                return View(
                    EnrollmentOptionsViewPath,
                    model);
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            try
            {
                var selectedClasses =
                    await _context.ClassSessions
                        .Include(session =>
                            session.Subject)
                        .Include(session =>
                            session.TutorUser)
                        .Where(session =>
                            model.SelectedClassSessionIds
                                .Contains(
                                    session.ClassSessionId))
                        .OrderBy(session =>
                            session.ClassSessionId)
                        .ToListAsync();

                if (selectedClasses.Count !=
                    model.SelectedClassSessionIds.Count)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedClassSessionIds),
                        "One or more selected classes " +
                        "could not be found.");

                    await PopulateSelectionViewModelAsync(
                        model);

                    return View(
                        EnrollmentOptionsViewPath,
                        model);
                }

                var cancelledDates = await _context.ClassLessonChanges.AsNoTracking()
                    .Where(x => model.SelectedClassSessionIds.Contains(x.ClassSessionId))
                    .Select(x => new { x.ClassSessionId, x.OriginalDate }).ToListAsync();
                var remainingByClass = selectedClasses.Select(c => new {
                    Class = c,
                    Dates = LateMonthEnrollmentPolicy.RemainingDates(model.RequestedStartDate!.Value,
                        c.DayOfWeek, ClassScheduleService.FirstDate(c), AcademicYearPolicy.ClassEnd(c),
                        c.StartTime, ClassScheduleService.Now, cancelledDates.Where(x => x.ClassSessionId == c.ClassSessionId).Select(x => x.OriginalDate))
                }).ToList();
                foreach (var item in remainingByClass.Where(x => x.Dates.Count == 0))
                    ModelState.AddModelError(nameof(model.RequestedStartDate),
                        $"{GetClassName(item.Class)} has no remaining regular lessons in the selected month. Choose a different start date or remove this class. Regular lessons run from the 1st to the 28th.");
                bool needsConfirmation = remainingByClass.Any(x => x.Dates.Count is 1 or 2);
                if (needsConfirmation && !model.FinalTwoWeeksConfirmed)
                    ModelState.AddModelError(nameof(model.FinalTwoWeeksConfirmed),
                        "Confirm the listed classes with only one or two regular lessons remaining and acknowledge the full monthly tuition.");
                // One explicit checkbox covers both the dates and existing full-month fee policy.
                model.FinalTwoWeeksConfirmed = needsConfirmation && model.FinalTwoWeeksConfirmed;
                model.FinalTwoWeeksFeeAcknowledged = model.FinalTwoWeeksConfirmed;
                if (!ModelState.IsValid)
                {
                    await PopulateSelectionViewModelAsync(model);
                    SetFeePreview(model);
                    return View(EnrollmentOptionsViewPath, model);
                }


                /*
                 * Check selected classes for schedule
                 * conflicts.
                 */
                var conflicts = FindSelectedScheduleConflicts(selectedClasses, model.RequestedStartDate!.Value);
                if (conflicts.Count > 0)
                {
                    ModelState.AddModelError(nameof(model.SelectedClassSessionIds),
                        $"There are {conflicts.Count} schedule overlap(s). Please change your class selection.");
                    await PopulateSelectionViewModelAsync(model);
                    return View(EnrollmentOptionsViewPath, model);
                }

                /*
                 * Check class status and capacity.
                 */
                foreach (var classSession
                         in selectedClasses)
                {
                    if (!AcademicYearPolicy.CanEnrollOn(classSession, model.RequestedStartDate!.Value))
                    {
                        ModelState.AddModelError(
                            nameof(
                                model
                                    .SelectedClassSessionIds),
                            $"Class " +
                            $"{GetClassName(classSession)} " +
                            "is unavailable for the requested enrollment start date.");

                        await PopulateSelectionViewModelAsync(
                            model);

                        return View(
                            EnrollmentOptionsViewPath,
                            model);
                    }

                    int occupiedSeats =
                        await new EnrollmentRulesService(_context).OccupiedSeatsAsync(classSession.ClassSessionId, startDate: model.RequestedStartDate ?? GetRequestedStartDate());

                    if (occupiedSeats >=
                        classSession.Capacity)
                    {
                        ModelState.AddModelError(
                            nameof(
                                model
                                    .SelectedClassSessionIds),
                            $"Class " +
                            $"{GetClassName(classSession)} " +
                            "is already full.");

                        await PopulateSelectionViewModelAsync(
                            model);

                        return View(
                            EnrollmentOptionsViewPath,
                            model);
                    }
                }

                /*
                 * Find an existing student using the IC number.
                 */
                var existingStudent =
                    await _context.StudentProfiles
                        .FirstOrDefaultAsync(student =>
                            student.IdentificationNumber ==
                                studentDetails
                                    .StudentIdentificationNumber);


                var ruleError = await new EnrollmentRulesService(_context).ValidateAsync(model.SelectedClassSessionIds.ToList(), model.RequestedStartDate!.Value.Date, existingStudent?.StudentProfileId, durationMonths: model.EnrollmentDurationMonths);
                if (ruleError != null) { ModelState.AddModelError("", ruleError); await PopulateSelectionViewModelAsync(model); return View(EnrollmentOptionsViewPath, model); }

                /*
                 * Check whether the student is already
                 * enrolled in a selected class.
                 */
                if (existingStudent != null)
                {
                    bool alreadyEnrolled =
                        await _context.StudentEnrollments
                            .AnyAsync(enrollment =>
                                enrollment.StudentProfileId ==
                                    existingStudent
                                        .StudentProfileId &&
                                model.SelectedClassSessionIds
                                    .Contains(
                                        enrollment
                                            .ClassSessionId) &&
                                (enrollment.Status ==
                                    StudentEnrollmentStatus
                                        .Active ||
                                 (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date))));

                    if (alreadyEnrolled)
                    {
                        ModelState.AddModelError(
                            nameof(
                                model
                                    .SelectedClassSessionIds),
                            "The student is already enrolled " +
                            "in one or more selected classes.");

                        await PopulateSelectionViewModelAsync(
                            model);

                        return View(
                            EnrollmentOptionsViewPath,
                            model);
                    }
                }

                /*
                 * Prevent the same student from submitting
                 * another active application for the same
                 * class.
                 */
                bool existingActiveApplication =
                    await _context.EnrollmentApplications
                        .AnyAsync(application =>
                            application.StudentIdentificationNumber ==
                                studentDetails.StudentIdentificationNumber &&
                            application.Status != EnrollmentStatus.Rejected &&
                            application.Status != EnrollmentStatus.Cancelled &&
                            application.SelectedSubjects.Any(selected =>
                                model.SelectedClassSessionIds.Contains(
                                    selected.ClassSessionId)));

                if (existingActiveApplication)
                {
                    ModelState.AddModelError(
                        nameof(model.SelectedClassSessionIds),
                        "An active enrollment application already exists " +
                        "for this student and one or more selected classes.");

                    await PopulateSelectionViewModelAsync(model);

                    return View(
                        EnrollmentOptionsViewPath,
                        model);
                }

                /*
                 * Resolve Guardian information.
                 */
                ParentProfile? existingParent = null;

                string guardianName;
                string guardianEmail;
                string guardianPhoneNumber;
                string? guardianIdentificationNumber;
                string? guardianIdentificationLast4;

                if (guardianDetails
                    .UseExistingParentAccount)
                {
                    string? existingParentUserId =
                        HttpContext.Session.GetString(
                            ExistingParentUserIdSessionKey);

                    if (string.IsNullOrWhiteSpace(
                        existingParentUserId))
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "The existing parent or guardian " +
                            "verification has expired. Please " +
                            "verify the account again.");

                        await PopulateSelectionViewModelAsync(
                            model);

                        return View(
                            EnrollmentOptionsViewPath,
                            model);
                    }

                    existingParent =
                        await _context.ParentProfiles
                            .Include(parent =>
                                parent.ApplicationUser)
                            .FirstOrDefaultAsync(parent =>
                                parent.ApplicationUserId ==
                                existingParentUserId);

                    if (existingParent == null ||
                        existingParent.ApplicationUser ==
                            null ||
                        !existingParent.ApplicationUser
                            .IsActive)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "The verified parent or guardian " +
                            "account is no longer available.");

                        await PopulateSelectionViewModelAsync(
                            model);

                        return View(
                            EnrollmentOptionsViewPath,
                            model);
                    }

                    guardianName =
                        existingParent.FullName;

                    guardianEmail =
                        existingParent.ApplicationUser
                            .Email ??
                        guardianDetails
                            .ExistingParentEmail ??
                        string.Empty;

                    guardianPhoneNumber =
                        existingParent.ApplicationUser
                            .PhoneNumber ??
                        "Not provided";

                    guardianIdentificationNumber =
                        existingParent
                            .IdentificationNumber;

                    guardianIdentificationLast4 =
                        existingParent
                            .IdentificationLast4;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(
                            guardianDetails.GuardianName) ||
                        string.IsNullOrWhiteSpace(
                            guardianDetails.GuardianEmail) ||
                        string.IsNullOrWhiteSpace(
                            guardianDetails
                                .GuardianPhoneNumber) ||
                        string.IsNullOrWhiteSpace(
                            guardianDetails
                                .GuardianIdentificationNumber))
                    {
                        return ReturnToGuardianDetails();
                    }

                    guardianName =
                        guardianDetails.GuardianName
                            .Trim();

                    guardianEmail =
                        guardianDetails.GuardianEmail
                            .Trim();

                    guardianPhoneNumber =
                        guardianDetails
                            .GuardianPhoneNumber
                            .Trim();

                    guardianIdentificationNumber =
                        guardianDetails
                            .GuardianIdentificationNumber
                            .Trim();

                    guardianIdentificationLast4 =
                        GetLastFourDigits(
                            guardianIdentificationNumber);
                }

                EnrollmentStatus initialStatus =
                    EnrollmentStatus.PendingReview;

                decimal monthlyTuitionFee =
                    selectedClasses.Sum(classSession =>
                        classSession.Subject.MonthlyFee);

                decimal discountRate =
                    GetDiscountRate(
                        model.EnrollmentDurationMonths);

                decimal tuitionSubtotal =
                    monthlyTuitionFee *
                    model.EnrollmentDurationMonths;

                decimal discountAmount =
                    Math.Round(
                        tuitionSubtotal * discountRate / 100m,
                        2,
                        MidpointRounding.AwayFromZero);

                decimal discountedTuition =
                    tuitionSubtotal - discountAmount;

                int hardcopySubjectCount =
                    model.HardcopyMaterialsRequested
                        ? selectedClasses
                            .Select(classSession => new
                            {
                                classSession.Subject
                                    .EducationStage,
                                AcademicLevel =
                                    classSession.Subject
                                        .AcademicLevel
                                        .Trim()
                                        .ToUpper(),
                                SubjectName =
                                    classSession.Subject
                                        .SubjectName
                                        .Trim()
                                        .ToUpper()
                            })
                            .Distinct()
                            .Count()
                        : 0;

                decimal hardcopyFee =
                    hardcopySubjectCount *
                    model.EnrollmentDurationMonths *
                    HardcopyFeePerSubject;

                decimal shippingFee =
                    GetShippingFee(
                        model.HardcopyMaterialsRequested,
                        model.DeliveryRegion);

                decimal estimatedTotal =
                    discountedTuition +
                    hardcopyFee +
                    shippingFee;

                string applicationNumber =
                    await GenerateNextApplicationNumberAsync();

                string? submittedByUserId =
                    User.Identity?.IsAuthenticated == true
                        ? _userManager.GetUserId(User)
                        : null;

                var application =
                    new EnrollmentApplication
                    {
                        ApplicationNumber =
                            applicationNumber,

                        SubmittedByUserId =
                            submittedByUserId,

                        ExistingParentProfileId =
                            existingParent?
                                .ParentProfileId,

                        ExistingStudentProfileId =
                            existingStudent?
                                .StudentProfileId,

                        GuardianName =
                            guardianName,

                        GuardianEmail =
                            guardianEmail,

                        GuardianPhoneNumber =
                            guardianPhoneNumber,

                        GuardianIdentificationNumber =
                            guardianIdentificationNumber,

                        GuardianIdentificationLast4 =
                            guardianIdentificationLast4,

                        StudentName =
                            studentDetails.StudentName
                                .Trim(),

                        StudentIdentificationNumber =
                            studentDetails
                                .StudentIdentificationNumber
                                .Trim(),

                        StudentDateOfBirth =
                            studentDetails
                                .StudentDateOfBirth!
                                .Value.Date,

                        StudentGender =
                            studentDetails.StudentGender
                                .Trim(),

                        StudentSchool =
                            studentDetails.StudentSchool
                                .Trim(),

                        StudentEmail =
                            studentDetails.StudentHasNoEmail
                                ? null
                                : studentDetails.StudentEmail?.Trim(),

                        StudentHasNoEmail =
                            studentDetails.StudentHasNoEmail,

                        // Student school level is not collected here. Preserve an existing
                        // student's level; do not infer a new student's level from a class.
                        AcademicLevel = string.IsNullOrWhiteSpace(existingStudent?.AcademicLevel)
                            ? "Not provided" : existingStudent.AcademicLevel.Trim(),

                        RequestedStartDate =
                            model.RequestedStartDate!
                                .Value.Date,

                        EnrollmentDurationMonths =
                            model.EnrollmentDurationMonths,

                        HardcopyMaterialsRequested =
                            model
                                .HardcopyMaterialsRequested,

                        DeliveryRecipientName =
                            CleanOptional(
                                model.DeliveryRecipientName),

                        DeliveryAddressLine1 =
                            CleanOptional(
                                model.DeliveryAddressLine1),

                        DeliveryAddressLine2 =
                            CleanOptional(
                                model.DeliveryAddressLine2),

                        DeliveryCity =
                            CleanOptional(model.DeliveryCity),

                        DeliveryState =
                            CleanOptional(model.DeliveryState),

                        DeliveryPostcode =
                            CleanOptional(
                                model.DeliveryPostcode),

                        DeliveryRegion =
                            CleanOptional(
                                model.DeliveryRegion),

                        MonthlyTuitionFeeAtApplication =
                            monthlyTuitionFee,

                        TuitionSubtotalAtApplication =
                            tuitionSubtotal,

                        DiscountRateAtApplication =
                            discountRate,

                        DiscountAmountAtApplication =
                            discountAmount,

                        DiscountedTuitionAtApplication =
                            discountedTuition,

                        HardcopySubjectCountAtApplication =
                            hardcopySubjectCount,

                        HardcopyFeeAtApplication =
                            hardcopyFee,

                        ShippingFeeAtApplication =
                            shippingFee,

                        EstimatedTotalAtApplication =
                            estimatedTotal,

                        FinalTwoWeeksConfirmed =
                            model
                                .FinalTwoWeeksConfirmed,

                        Status = initialStatus,

                        ApplicantNotes =
                            string.IsNullOrWhiteSpace(
                                model.ApplicantNotes)
                                ? null
                                : model.ApplicantNotes
                                    .Trim(),

                        SubmittedAt =
                            DateTime.UtcNow
                    };

                foreach (var classSession
                         in selectedClasses)
                {
                    application.SelectedSubjects.Add(
                        new EnrollmentApplicationSubject
                        {
                            ClassSessionId =
                                classSession
                                    .ClassSessionId,

                            MonthlyFeeAtApplication =
                                classSession
                                    .Subject
                                    .MonthlyFee
                        });
                }

                var duplicateCandidates = await new StudentDuplicateReviewService(_context)
                    .FindAsync(application.StudentName, application.StudentDateOfBirth,
                        application.StudentIdentificationNumber, application.ExistingStudentProfileId,
                        cancellationToken: HttpContext.RequestAborted);

                application.StatusHistories.Add(
                    new EnrollmentStatusHistory
                    {
                        PreviousStatus =
                            null,

                        NewStatus =
                            initialStatus,

                        ChangedAt =
                            application.SubmittedAt,

                        ChangedByUserId =
                            submittedByUserId,

                        Remarks = duplicateCandidates.Count > 0
                            ? "Enrollment application submitted. Possible duplicate student detected by same-birthdate Jaro-Winkler comparison; administrator review required."
                            : "Enrollment application submitted."
                    });

                _context.EnrollmentApplications.Add(
                    application);
                await _notices.QueueAsync(application, rejected: false);

                await _context.SaveChangesAsync();

                if (consentParent != null && application.ExistingParentProfileId == consentParent.ParentProfileId)
                {
                    var currentConsent = await enrollmentVerification.GetVerifiedAsync(HttpContext);
                    if (currentConsent == null || currentConsent.ParentProfileId != consentParent.ParentProfileId
                        || currentConsent.ApplicationUser.Email != application.GuardianEmail)
                        throw new InvalidOperationException("Guardian consent expired or changed. Please verify the guardian email again.");
                    enrollmentVerification.RecordConsent(application, consentParent.ApplicationUserId);
                    await _context.SaveChangesAsync();
                }
                await transaction.CommitAsync();

                HttpContext.Session.SetInt32(
                    LastSubmittedApplicationIdSessionKey,
                    application
                        .EnrollmentApplicationId);

                if (addStudentId.HasValue) HttpContext.Session.SetInt32("Enrollment.LastAdditionalApplicationId", application.EnrollmentApplicationId);
                else HttpContext.Session.Remove("Enrollment.LastAdditionalApplicationId");
                ClearEnrollmentDraftSession();

                return RedirectToAction(
                    nameof(Submitted),
                    new
                    {
                        id =
                            application
                                .EnrollmentApplicationId
                    });
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,

                    "An application operation failed. Refer to the private server log using the trace ID.");

                _logger.LogError(
                    exception,
                    "Enrollment submission failed. Trace {TraceId}.",
                    HttpContext.TraceIdentifier);

                ModelState.AddModelError(
                    string.Empty,
                    "The enrollment application could not " +
                    "be submitted. Please try again.");

                await PopulateSelectionViewModelAsync(
                    model);

                return View(
                    EnrollmentOptionsViewPath,
                    model);
            }
        }

        /*
         * =====================================================
         * SUBMISSION CONFIRMATION PAGE
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> Submitted(int id)
        {
            int? submittedApplicationId =
                HttpContext.Session.GetInt32(
                    LastSubmittedApplicationIdSessionKey);

            bool isAdmin =
                User.Identity?.IsAuthenticated == true &&
                User.IsInRole("Admin");

            if (submittedApplicationId != id &&
                !isAdmin)
            {
                return NotFound();
            }

            var application =
                await _context.EnrollmentApplications
                    .AsNoTracking()
                    .Where(item =>
                        item.EnrollmentApplicationId ==
                        id)
                    .Select(item =>
                        new EnrollmentSubmittedViewModel
                        {
                            EnrollmentApplicationId =
                                item
                                    .EnrollmentApplicationId,

                            ApplicationNumber =
                                item.ApplicationNumber,

                            StudentName =
                                item.StudentName,

                            GuardianEmail =
                                item.GuardianEmail,

                            GuardianName =
                                item.GuardianName,

                            SelectedClassCount =
                                item.SelectedSubjects.Count,

                            SelectedClassNames = item.SelectedSubjects.OrderBy(x => x.EnrollmentApplicationSubjectId)
                                .Select(x => x.ClassSession.ClassName ?? x.ClassSession.ClassCode).ToList(),

                            TotalMonthlyFee =
                                item.MonthlyTuitionFeeAtApplication,

                            EnrollmentDurationMonths =
                                item.EnrollmentDurationMonths,

                            DiscountRate =
                                item.DiscountRateAtApplication,

                            EstimatedTotal =
                                item.EstimatedTotalAtApplication,

                            RequestedStartDate =
                                item.RequestedStartDate,

                            Status =
                                item.Status,

                            SubmittedAt =
                                item.SubmittedAt
                        })
                    .FirstOrDefaultAsync();

            if (application == null)
            {
                return NotFound();
            }

            ViewData["IsAdditionalEnrollment"] = HttpContext.Session.GetInt32("Enrollment.LastAdditionalApplicationId") == id;
            return View(
                SubmittedViewPath,
                application);
        }

        /*
         * =====================================================
         * LOAD AVAILABLE CLASSES
         * =====================================================
         */

        private async Task PopulateSelectionViewModelAsync(
            EnrollmentSelectionViewModel model)
        {
            var classSessions =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session =>
                        session.Subject)
                    .Include(session =>
                        session.TutorUser)
                    .Where(session =>
                        session.Status ==
                            ClassSessionStatus.Active &&
                        (!session.EndDate.HasValue || session.EndDate.Value >= DateTime.UtcNow.AddHours(8).Date) &&
                        (session.StartDate ?? session.CreatedAt.AddHours(8)).Year >= DateTime.UtcNow.AddHours(8).Year &&
                        session.Subject.IsActive)
                    .OrderBy(session =>
                        session.Subject.EducationStage)
                    .ThenBy(session =>
                        session.Subject.AcademicLevel)
                    .ThenBy(session =>
                        session.Subject.SubjectName)
                    .ThenBy(session =>
                        session.DayOfWeek)
                    .ThenBy(session =>
                        session.StartTime)
                    .ToListAsync();

            int? addStudentId = HttpContext.Session.GetInt32("Enrollment.AddSubjectStudentId");
            if (addStudentId.HasValue)
            {
                string? userId = _userManager.GetUserId(User);
                bool ownsStudent = userId != null && await _context.StudentProfiles.AnyAsync(x => x.StudentProfileId == addStudentId.Value && x.IsActive &&
                    (User.IsInRole("Admin") || (User.IsInRole("Student") && x.ApplicationUserId == userId) ||
                     (User.IsInRole("Parent") && x.ParentStudents.Any(link => link.IsActive && link.ParentProfile.ApplicationUserId == userId))));
                if (!ownsStudent) classSessions.Clear();
            }

            var availability = await EnrollmentAvailability.LoadAsync(_context,classSessions.Select(x=>x.ClassSessionId).ToList(),addStudentId);
            var classIds = classSessions.Select(x => x.ClassSessionId).ToList();
            var cancelled = await _context.ClassLessonChanges.AsNoTracking()
                .Where(x => classIds.Contains(x.ClassSessionId))
                .Select(x => new { x.ClassSessionId, x.OriginalDate }).ToListAsync();
            var availableClasses =
                new List<
                    EnrollmentSelectableClassViewModel>();

            foreach (var classSession
                     in classSessions)
            {
                int occupiedSeats = availability.PeakSeats(classSession.ClassSessionId,
                    model.RequestedStartDate ?? GetRequestedStartDate(), AcademicYearPolicy.ClassEnd(classSession));
                var dates = new Dictionary<string,int>();
                var from = ClassScheduleService.Today;
                var until = AcademicYearPolicy.ClassEnd(classSession);
                if (until > from.AddYears(1)) until = from.AddYears(1);
                for(var day=from;day<=until;day=day.AddDays(1)) {
                    if (!AcademicYearPolicy.CanEnrollOn(classSession, day)) continue;
                    if (LateMonthEnrollmentPolicy.RemainingDates(day, classSession.DayOfWeek,
                        ClassScheduleService.FirstDate(classSession), AcademicYearPolicy.ClassEnd(classSession),
                        classSession.StartTime, ClassScheduleService.Now,
                        cancelled.Where(x => x.ClassSessionId == classSession.ClassSessionId).Select(x => x.OriginalDate)).Count == 0)
                    { dates[day.ToString("yyyy-MM-dd")] = -2; continue; }
                    var blocked=addStudentId.HasValue&&availability.SubjectBlocked(addStudentId.Value,classSession.SubjectId,day,AcademicYearPolicy.ClassEnd(classSession));
                    dates[day.ToString("yyyy-MM-dd")]=blocked?-1:Math.Max(0,classSession.Capacity-availability.PeakSeats(classSession.ClassSessionId,day,AcademicYearPolicy.ClassEnd(classSession)));
                }
                availableClasses.Add(
                    new EnrollmentSelectableClassViewModel
                    {
                        AvailabilityByDate = dates,
                        ClassSessionId =
                            classSession.ClassSessionId,
                        TermStart = ClassScheduleService.FirstDate(classSession),
                        TermEnd = AcademicYearPolicy.ClassEnd(classSession),

                        ClassCode =
                            classSession.ClassCode,

                        ClassName =
                            GetClassName(classSession),

                        SubjectName =
                            classSession.Subject
                                .SubjectName,

                        EducationStage =
                            classSession.Subject
                                .EducationStage
                                .ToString(),

                        AcademicLevel =
                            classSession.Subject
                                .AcademicLevel,

                        TutorName =
                            classSession.TutorUser
                                .FullName ??
                            classSession.TutorUser
                                .Email ??
                            "Tutor",

                        Day =
                            classSession.DayOfWeek
                                .ToString(),

                        StartTime =
                            classSession.StartTime
                                .ToString("hh:mm tt"),

                        EndTime =
                            classSession.EndTime
                                .ToString("hh:mm tt"),

                        MonthlyFee =
                            classSession.Subject
                                .MonthlyFee,

                        Capacity =
                            classSession.Capacity,

                        StudentCount =
                            occupiedSeats,

                        Status =
                            classSession.Status
                    });
            }


            foreach (var item in availableClasses)
                item.CancelledDates = cancelled.Where(x => x.ClassSessionId == item.ClassSessionId)
                    .Select(x => x.OriginalDate.ToString("yyyy-MM-dd")).ToList();
            model.AvailableClasses = availableClasses;
            var selectedTerms = await _context.ClassSessions.AsNoTracking().Include(x => x.Subject).Where(x => model.SelectedClassSessionIds.Contains(x.ClassSessionId)).ToListAsync();
            model.ScheduleConflicts = FindSelectedScheduleConflicts(selectedTerms,
                model.RequestedStartDate ?? GetRequestedStartDate());
            model.SelectedTermEndDate = selectedTerms.Count > 0 ? selectedTerms.Select(AcademicYearPolicy.BillableEnd).Min() : null;

            model.AcademicLevelOptions =
                classSessions
                    .Select(session =>
                        session.Subject.AcademicLevel)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(level => level)
                    .ToList();

            model.SubjectOptions =
                classSessions
                    .Select(session =>
                        session.Subject.SubjectName)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(subject => subject)
                    .ToList();
        }

        /*
         * =====================================================
         * GENERATE EA00001
         * =====================================================
         */

        private async Task<string>
            GenerateNextApplicationNumberAsync()
        {
            var applicationNumbers =
                await _context.EnrollmentApplications
                    .AsNoTracking()
                    .Where(application =>
                        application.ApplicationNumber
                            .StartsWith("EA") &&
                        application.ApplicationNumber
                            .Length == 7)
                    .Select(application =>
                        application.ApplicationNumber)
                    .ToListAsync();

            int highestNumber = 0;

            foreach (string applicationNumber
                     in applicationNumbers)
            {
                string numericPart =
                    applicationNumber.Substring(2);

                if (int.TryParse(
                        numericPart,
                        out int currentNumber) &&
                    currentNumber > highestNumber)
                {
                    highestNumber =
                        currentNumber;
                }
            }

            int nextNumber =
                highestNumber + 1;

            if (nextNumber > 99999)
            {
                throw new InvalidOperationException(
                    "The enrollment application number " +
                    "limit has been reached.");
            }

            return $"EA{nextNumber:00000}";
        }

        /*
         * =====================================================
         * SESSION HELPERS
         * =====================================================
         */

        private StudentDetailsViewModel?
            GetStudentDetailsFromSession()
        {
            string? json =
                HttpContext.Session.GetString(
                    StudentDetailsSessionKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<
                StudentDetailsViewModel>(json);
        }

        private GuardianDetailsViewModel?
            GetGuardianDetailsFromSession()
        {
            string? json =
                HttpContext.Session.GetString(
                    GuardianDetailsSessionKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<
                GuardianDetailsViewModel>(json);
        }

        private void ClearEnrollmentDraftSession()
        {
            HttpContext.Session.Remove("Enrollment.GuardianOtp.Pending");
            HttpContext.Session.Remove("Enrollment.GuardianOtp.Proof");
            HttpContext.Session.Remove("Enrollment.AddSubjectStudentId");
            HttpContext.Session.Remove("Enrollment.RequestedStartDate");
            HttpContext.Session.Remove(
                StudentDetailsSessionKey);

            HttpContext.Session.Remove(
                GuardianDetailsSessionKey);

            HttpContext.Session.Remove(
                ExistingParentUserIdSessionKey);

            HttpContext.Session.Remove(
                SelectedClassIdsSessionKey);
        }

        private IActionResult ReturnToStudentDetails()
        {
            TempData["EnrollmentMessage"] =
                "Please complete the student details " +
                "before selecting classes.";

            return RedirectToAction(
                "StudentDetails",
                "Enrollment");
        }

        private IActionResult ReturnToGuardianDetails()
        {
            TempData["EnrollmentMessage"] =
                "Please complete the parent or guardian " +
                "details before selecting classes.";

            return RedirectToAction(
                "GuardianDetails",
                "Enrollment");
        }

        /*
         * =====================================================
         * GENERAL HELPERS
         * =====================================================
         */

        private DateTime GetRequestedStartDate()
        {
            var today = ClassScheduleService.Today;

            var savedDate = HttpContext.Session.GetString(
                "Enrollment.RequestedStartDate");

            if (DateTime.TryParseExact(
                    savedDate,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date)
                && date.Date >= today
                && date.Date <= today.AddYears(1))
            {
                return date.Date;
            }

            return today;
        }

        private static string GetGuardianDisplayName(
            GuardianDetailsViewModel guardianDetails)
        {
            if (guardianDetails
                .UseExistingParentAccount)
            {
                return guardianDetails
                           .VerifiedParentName ??
                       "Existing Parent / Guardian";
            }

            return guardianDetails.GuardianName ??
                   "Parent / Guardian";
        }

        private static List<EnrollmentScheduleConflictViewModel> FindSelectedScheduleConflicts(List<ClassSession> classes, DateTime requestedStart)
        {
            var conflicts = new List<EnrollmentScheduleConflictViewModel>();
            for (int i = 0; i < classes.Count; i++)
            {
                var first = classes[i];
                var firstStart = ClassScheduleService.FirstDate(first) > requestedStart.Date
                    ? ClassScheduleService.FirstDate(first) : requestedStart.Date;
                for (int j = i + 1; j < classes.Count; j++)
                {
                    var second = classes[j];
                    var secondStart = ClassScheduleService.FirstDate(second) > requestedStart.Date
                        ? ClassScheduleService.FirstDate(second) : requestedStart.Date;
                    if (ClassScheduleService.WeeklyOverlap(firstStart, AcademicYearPolicy.ClassEnd(first),
                        first.DayOfWeek, first.StartTime, first.EndTime,
                        secondStart, AcademicYearPolicy.ClassEnd(second), second.DayOfWeek, second.StartTime, second.EndTime))
                    {
                        string DisplayName(ClassSession c)
                        {
                            var name = GetClassName(c);
                            var suffix = $" - {c.DayOfWeek} - {c.StartTime.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)}";
                            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name[..^suffix.Length] : name;
                        }
                        conflicts.Add(new EnrollmentScheduleConflictViewModel
                        {
                            PairTitle = $"{first.Subject?.AcademicLevel ?? DisplayName(first)} & {second.Subject?.AcademicLevel ?? DisplayName(second)}",
                            FirstClassName = DisplayName(first), SecondClassName = DisplayName(second), Day = first.DayOfWeek,
                            FirstStart = first.StartTime, FirstEnd = first.EndTime,
                            SecondStart = second.StartTime, SecondEnd = second.EndTime,
                            OverlapStart = first.StartTime > second.StartTime ? first.StartTime : second.StartTime,
                            OverlapEnd = first.EndTime < second.EndTime ? first.EndTime : second.EndTime
                        });
                    }
                }
            }
            return conflicts;
        }

        private static string GetClassName(
            ClassSession classSession)
        {
            return string.IsNullOrWhiteSpace(
                classSession.ClassName)
                ? "Class"
                : classSession.ClassName;
        }

        private static string? GetLastFourDigits(
            string? identificationNumber)
        {
            if (string.IsNullOrWhiteSpace(
                identificationNumber))
            {
                return null;
            }

            string digits =
                new string(
                    identificationNumber
                        .Where(char.IsDigit)
                        .ToArray());

            if (digits.Length < 4)
            {
                return null;
            }

            return digits.Substring(
                digits.Length - 4,
                4);
        }

        private List<int> GetSelectedClassIdsFromSession()
        {
            string? json = HttpContext.Session.GetString(
                SelectedClassIdsSessionKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<int>();
            }

            return JsonSerializer.Deserialize<List<int>>(json)
                ?? new List<int>();
        }

        private void SaveSelectedClassIdsToSession(
            List<int> selectedIds)
        {
            HttpContext.Session.SetString(
                SelectedClassIdsSessionKey,
                JsonSerializer.Serialize(selectedIds));
        }

        private void SetFeePreview(
            EnrollmentSelectionViewModel model)
        {
            var selected = model.AvailableClasses
                .Where(item => model.SelectedClassSessionIds
                    .Contains(item.ClassSessionId))
                .ToList();

            model.SelectedClassCount = selected.Count;
            model.UniqueSubjectCount = selected
                .Select(item => $"{item.EducationStage}|{item.AcademicLevel}|{item.SubjectName}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            model.MonthlyTuitionFee = selected.Sum(item => item.MonthlyFee);
            model.DiscountRate = GetDiscountRate(model.EnrollmentDurationMonths);
            model.TuitionSubtotal = model.MonthlyTuitionFee * model.EnrollmentDurationMonths;
            model.DiscountAmount = Math.Round(
                model.TuitionSubtotal * model.DiscountRate / 100m, 2);
            model.DiscountedTuition = model.TuitionSubtotal - model.DiscountAmount;
            model.HardcopyFee =
                model.HardcopyMaterialsRequested
                    ? model.UniqueSubjectCount *
                      model.EnrollmentDurationMonths *
                      HardcopyFeePerSubject
                    : 0m;
            model.ShippingFee = model.HardcopyMaterialsRequested &&
                !string.IsNullOrWhiteSpace(model.DeliveryRegion)
                    ? GetShippingFee(true, model.DeliveryRegion)
                    : 0m;
            model.EstimatedTotal = model.DiscountedTuition +
                model.HardcopyFee + model.ShippingFee;
        }

        private static decimal GetDiscountRate(
            int enrollmentDurationMonths)
        {
            return enrollmentDurationMonths switch
            {
                1 => 0m,
                2 => 0m,
                3 => 3m,
                6 => 5m,
                9 => 8m,
                12 => 10m,
                _ => 0m
            };
        }

        private static decimal GetShippingFee(
            bool hardcopyRequested,
            string? deliveryRegion)
        {
            if (!hardcopyRequested)
            {
                return 0m;
            }

            return deliveryRegion?.Trim() switch
            {
                "West Malaysia" =>
                    WestMalaysiaShippingFee,

                "East Malaysia" =>
                    EastMalaysiaShippingFee,

                _ => throw new InvalidOperationException(
                    "Invalid delivery region.")
            };
        }

        private static void NormalizeDeliveryFields(
            EnrollmentSelectionViewModel model)
        {
            if (model.HardcopyMaterialsRequested)
            {
                model.DeliveryRegion =
                    IsEastMalaysiaState(model.DeliveryState)
                        ? "East Malaysia"
                        : "West Malaysia";
                return;
            }

            model.DeliveryRecipientName = null;
            model.DeliveryAddressLine1 = null;
            model.DeliveryAddressLine2 = null;
            model.DeliveryCity = null;
            model.DeliveryState = null;
            model.DeliveryPostcode = null;
            model.DeliveryRegion = null;
        }

        private static bool IsEastMalaysiaState(string? state)
        {
            return state?.Trim() is
                "Sabah" or "Sarawak" or "Labuan";
        }

        private static string? CleanOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
