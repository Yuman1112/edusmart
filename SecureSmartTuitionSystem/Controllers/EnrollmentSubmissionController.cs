using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [AllowAnonymous]
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

        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;

        public EnrollmentSubmissionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

                    RequestedStartDate =
                        DateTime.Today.AddDays(7),

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
            if (GetStudentDetailsFromSession() == null)
            {
                return ReturnToStudentDetails();
            }

            if (GetGuardianDetailsFromSession() == null)
            {
                return ReturnToGuardianDetails();
            }

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

            var selectedClasses =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        model.SelectedClassSessionIds.Contains(
                            session.ClassSessionId) &&
                        session.Status == ClassSessionStatus.Active &&
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

            bool mixedStage = selectedClasses
                .Select(item => item.Subject.EducationStage)
                .Distinct()
                .Count() > 1;

            bool mixedLevel = selectedClasses
                .Select(item => item.Subject.AcademicLevel.Trim().ToUpper())
                .Distinct()
                .Count() > 1;

            if (mixedStage || mixedLevel)
            {
                ModelState.AddModelError(
                    nameof(model.SelectedClassSessionIds),
                    "All selected classes must have the same education stage and academic level.");

                await PopulateSelectionViewModelAsync(model);
                return View(SelectClassesViewPath, model);
            }

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
                RequestedStartDate = DateTime.Today.AddDays(7),
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

            model.StudentName =
                studentDetails.StudentName;

            model.GuardianName =
                GetGuardianDisplayName(
                    guardianDetails);

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

                bool mixedEducationStage =
                    selectedClasses
                        .Select(classSession =>
                            classSession.Subject
                                .EducationStage)
                        .Distinct()
                        .Count() > 1;

                bool mixedAcademicLevel =
                    selectedClasses
                        .Select(classSession =>
                            classSession.Subject
                                .AcademicLevel
                                .Trim()
                                .ToUpper())
                        .Distinct()
                        .Count() > 1;

                if (mixedEducationStage ||
                    mixedAcademicLevel)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedClassSessionIds),
                        "All selected classes must belong " +
                        "to the same education stage and " +
                        "academic level.");

                    await PopulateSelectionViewModelAsync(
                        model);

                    return View(
                        EnrollmentOptionsViewPath,
                        model);
                }

                /*
                 * Check selected classes for schedule
                 * conflicts.
                 */
                for (int firstIndex = 0;
                     firstIndex < selectedClasses.Count;
                     firstIndex++)
                {
                    for (int secondIndex =
                             firstIndex + 1;
                         secondIndex <
                             selectedClasses.Count;
                         secondIndex++)
                    {
                        var firstClass =
                            selectedClasses[firstIndex];

                        var secondClass =
                            selectedClasses[secondIndex];

                        bool sameDay =
                            firstClass.DayOfWeek ==
                            secondClass.DayOfWeek;

                        bool timeOverlaps =
                            firstClass.StartTime <
                                secondClass.EndTime &&
                            secondClass.StartTime <
                                firstClass.EndTime;

                        if (sameDay &&
                            timeOverlaps)
                        {
                            ModelState.AddModelError(
                                nameof(
                                    model
                                        .SelectedClassSessionIds),
                                $"Classes " +
                                $"{GetClassName(firstClass)} " +
                                $"and " +
                                $"{GetClassName(secondClass)} " +
                                "have overlapping schedules.");

                            await PopulateSelectionViewModelAsync(
                                model);

                            return View(
                                EnrollmentOptionsViewPath,
                                model);
                        }
                    }
                }

                /*
                 * Check class status and capacity.
                 */
                foreach (var classSession
                         in selectedClasses)
                {
                    if (classSession.Status !=
                        ClassSessionStatus.Active)
                    {
                        ModelState.AddModelError(
                            nameof(
                                model
                                    .SelectedClassSessionIds),
                            $"Class " +
                            $"{GetClassName(classSession)} " +
                            "is no longer active.");

                        await PopulateSelectionViewModelAsync(
                            model);

                        return View(
                            EnrollmentOptionsViewPath,
                            model);
                    }

                    int occupiedSeats =
                        await _context.StudentEnrollments
                            .CountAsync(enrollment =>
                                enrollment.ClassSessionId ==
                                    classSession
                                        .ClassSessionId &&
                                (enrollment.Status ==
                                    StudentEnrollmentStatus
                                        .Active ||
                                 enrollment.Status ==
                                    StudentEnrollmentStatus
                                        .PendingDrop));

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
                 * Find an existing student using the
                 * identification type and number.
                 */
                var existingStudent =
                    await _context.StudentProfiles
                        .FirstOrDefaultAsync(student =>
                            student.IdentificationType ==
                                studentDetails
                                    .StudentIdentificationType &&
                            student.IdentificationNumber ==
                                studentDetails
                                    .StudentIdentificationNumber);

                bool potentialDuplicateStudent =
                    existingStudent != null;

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
                                 enrollment.Status ==
                                    StudentEnrollmentStatus
                                        .PendingDrop));

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
                bool duplicatePendingApplication =
                    await _context
                        .EnrollmentApplications
                        .AnyAsync(application =>
                            application
                                .StudentIdentificationNumber ==
                                studentDetails
                                    .StudentIdentificationNumber &&
                            application.Status !=
                                EnrollmentStatus.Rejected &&
                            application.Status !=
                                EnrollmentStatus.Cancelled &&
                            application.SelectedSubjects
                                .Any(selected =>
                                    model
                                        .SelectedClassSessionIds
                                        .Contains(
                                            selected
                                                .ClassSessionId)));

                if (duplicatePendingApplication)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedClassSessionIds),
                        "An active enrollment application " +
                        "already exists for this student " +
                        "and one or more selected classes.");

                    await PopulateSelectionViewModelAsync(
                        model);

                    return View(
                        EnrollmentOptionsViewPath,
                        model);
                }

                /*
                 * Resolve Guardian information.
                 */
                ParentProfile? existingParent = null;

                bool potentialDuplicateGuardian = false;

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

                    string normalizedEmail =
                        _userManager.NormalizeEmail(
                            guardianEmail);

                    potentialDuplicateGuardian =
                        await _context.ParentProfiles
                            .AnyAsync(parent =>
                                parent.ApplicationUser
                                    .NormalizedEmail ==
                                    normalizedEmail ||
                                parent
                                    .IdentificationNumber ==
                                    guardianIdentificationNumber);
                }

                EnrollmentStatus initialStatus =
                    potentialDuplicateStudent ||
                    potentialDuplicateGuardian
                        ? EnrollmentStatus
                            .DuplicateReview
                        : EnrollmentStatus
                            .PendingReview;

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

                        StudentIdentificationType =
                            studentDetails
                                .StudentIdentificationType
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

                        AcademicLevel =
                            selectedClasses[0]
                                .Subject
                                .AcademicLevel,

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

                        PotentialDuplicateStudent =
                            potentialDuplicateStudent,

                        PotentialDuplicateGuardian =
                            potentialDuplicateGuardian,

                        Status =
                            initialStatus,

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

                        Remarks =
                            initialStatus ==
                            EnrollmentStatus
                                .DuplicateReview
                                ? "Application submitted. " +
                                  "Potential duplicate " +
                                  "record detected."
                                : "Enrollment application " +
                                  "submitted."
                    });

                _context.EnrollmentApplications.Add(
                    application);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                HttpContext.Session.SetInt32(
                    LastSubmittedApplicationIdSessionKey,
                    application
                        .EnrollmentApplicationId);

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
            catch
            {
                await transaction.RollbackAsync();

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

            var availableClasses =
                new List<
                    EnrollmentSelectableClassViewModel>();

            foreach (var classSession
                     in classSessions)
            {
                int occupiedSeats =
                    await _context.StudentEnrollments
                        .AsNoTracking()
                        .CountAsync(enrollment =>
                            enrollment.ClassSessionId ==
                                classSession
                                    .ClassSessionId &&
                            (enrollment.Status ==
                                StudentEnrollmentStatus
                                    .Active ||
                             enrollment.Status ==
                                StudentEnrollmentStatus
                                    .PendingDrop));

                availableClasses.Add(
                    new EnrollmentSelectableClassViewModel
                    {
                        ClassSessionId =
                            classSession.ClassSessionId,

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

            model.AvailableClasses =
                availableClasses;

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

        private static string GetClassName(
            ClassSession classSession)
        {
            return string.IsNullOrWhiteSpace(
                classSession.ClassName)
                ? classSession.ClassCode
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
                3 => 3m,
                6 => 5m,
                9 => 8m,
                12 => 10m,
                _ => throw new InvalidOperationException(
                    "Invalid enrollment duration.")
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
