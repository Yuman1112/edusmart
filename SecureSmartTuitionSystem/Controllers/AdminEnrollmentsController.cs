using System.Data;
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
    [Authorize(Roles = "Admin")]
    public class AdminEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminEnrollmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /*
         * =====================================================
         * ENROLLMENT APPLICATION LIST
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> Index(
            EnrollmentStatus? status,
            bool duplicateOnly = false,
            string sortBy = "submittedAt",
            string sortDirection = "desc")
        {
            sortBy = NormalizeSortBy(sortBy);
            sortDirection = NormalizeSortDirection(sortDirection);

            var baseQuery = _context.EnrollmentApplications
                .AsNoTracking();

            int totalApplicationCount =
                await baseQuery.CountAsync();

            int pendingReviewCount =
                await baseQuery.CountAsync(application =>
                    application.Status ==
                    EnrollmentStatus.PendingReview);

            int duplicateReviewCount =
                await baseQuery.CountAsync(application =>
                    application.Status ==
                    EnrollmentStatus.DuplicateReview);

            int approvedPendingPaymentCount =
                await baseQuery.CountAsync(application =>
                    application.Status ==
                    EnrollmentStatus.ApprovedPendingPayment);

            int enrolledCount =
                await baseQuery.CountAsync(application =>
                    application.Status ==
                    EnrollmentStatus.Enrolled);

            var query = baseQuery.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(application =>
                    application.Status == status.Value);
            }

            if (duplicateOnly)
            {
                query = query.Where(application =>
                    application.PotentialDuplicateStudent ||
                    application.PotentialDuplicateGuardian);
            }

            query = ApplySorting(
                query,
                sortBy,
                sortDirection);

            var applications = await query
                .Select(application =>
                    new AdminEnrollmentListItemViewModel
                    {
                        EnrollmentApplicationId =
                            application.EnrollmentApplicationId,

                        ApplicationNumber =
                            application.ApplicationNumber,

                        StudentName =
                            application.StudentName,

                        AcademicLevel =
                            application.AcademicLevel,

                        GuardianName =
                            application.GuardianName,

                        GuardianEmail =
                            application.GuardianEmail,

                        SelectedClassCount =
                            application.SelectedSubjects.Count,

                        TotalMonthlyFee =
                            application.SelectedSubjects.Sum(
                                selected =>
                                    (decimal?)
                                    selected.MonthlyFeeAtApplication)
                            ?? 0,

                        PotentialDuplicateStudent =
                            application.PotentialDuplicateStudent,

                        PotentialDuplicateGuardian =
                            application.PotentialDuplicateGuardian,

                        Status = application.Status,

                        SubmittedAt =
                            application.SubmittedAt,

                        ReviewedAt =
                            application.ReviewedAt
                    })
                .ToListAsync();

            var viewModel =
                new AdminEnrollmentIndexViewModel
                {
                    Applications = applications,

                    Status = status,

                    DuplicateOnly = duplicateOnly,

                    SortBy = sortBy,

                    SortDirection = sortDirection,

                    TotalApplicationCount =
                        totalApplicationCount,

                    PendingReviewCount =
                        pendingReviewCount,

                    DuplicateReviewCount =
                        duplicateReviewCount,

                    ApprovedPendingPaymentCount =
                        approvedPendingPaymentCount,

                    EnrolledCount =
                        enrolledCount
                };

            return View(viewModel);
        }

        /*
         * =====================================================
         * ENROLLMENT APPLICATION DETAILS
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var application =
                await _context.EnrollmentApplications
                    .AsNoTracking()
                    .Include(item => item.ReviewedByUser)
                    .Include(item => item.SelectedSubjects)
                        .ThenInclude(selected =>
                            selected.ClassSession)
                        .ThenInclude(session =>
                            session.Subject)
                    .Include(item => item.SelectedSubjects)
                        .ThenInclude(selected =>
                            selected.ClassSession)
                        .ThenInclude(session =>
                            session.TutorUser)
                    .Include(item => item.StatusHistories)
                        .ThenInclude(history =>
                            history.ChangedByUser)
                    .FirstOrDefaultAsync(item =>
                        item.EnrollmentApplicationId == id);

            if (application == null)
            {
                return NotFound();
            }

            var classItems =
                new List<AdminEnrollmentClassItemViewModel>();

            foreach (var selected in
                     application.SelectedSubjects)
            {
                var classSession =
                    selected.ClassSession;

                int enrolledStudentCount =
                    await _context.StudentEnrollments
                        .AsNoTracking()
                        .CountAsync(enrollment =>
                            enrollment.ClassSessionId ==
                            classSession.ClassSessionId &&
                            (enrollment.Status ==
                                StudentEnrollmentStatus.Active ||
                             enrollment.Status ==
                                StudentEnrollmentStatus.PendingDrop));

                classItems.Add(
                    new AdminEnrollmentClassItemViewModel
                    {
                        EnrollmentApplicationSubjectId =
                            selected
                                .EnrollmentApplicationSubjectId,

                        ClassSessionId =
                            classSession.ClassSessionId,

                        ClassName =
                            classSession.ClassName ??
                            classSession.ClassCode,

                        SubjectName =
                            classSession.Subject.SubjectName,

                        EducationStage =
                            classSession.Subject
                                .EducationStage.ToString(),

                        AcademicLevel =
                            classSession.Subject.AcademicLevel,

                        TutorName =
                            classSession.TutorUser.FullName ??
                            classSession.TutorUser.Email ??
                            "Tutor",

                        Day =
                            classSession.DayOfWeek.ToString(),

                        StartTime =
                            classSession.StartTime
                                .ToString("hh:mm tt"),

                        EndTime =
                            classSession.EndTime
                                .ToString("hh:mm tt"),

                        MonthlyFeeAtApplication =
                            selected.MonthlyFeeAtApplication,

                        Capacity =
                            classSession.Capacity,

                        EnrolledStudentCount =
                            enrolledStudentCount,

                        ClassStatus =
                            classSession.Status
                    });
            }

            var histories =
                application.StatusHistories
                    .OrderByDescending(history =>
                        history.ChangedAt)
                    .Select(history =>
                        new AdminEnrollmentHistoryItemViewModel
                        {
                            EnrollmentStatusHistoryId =
                                history.EnrollmentStatusHistoryId,

                            PreviousStatus =
                                history.PreviousStatus,

                            NewStatus =
                                history.NewStatus,

                            ChangedAt =
                                history.ChangedAt,

                            ChangedByName =
                                history.ChangedByUser?.FullName ??
                                history.ChangedByUser?.Email ??
                                "System",

                            Remarks =
                                history.Remarks
                        })
                    .ToList();

            var viewModel =
                new AdminEnrollmentDetailsViewModel
                {
                    EnrollmentApplicationId =
                        application.EnrollmentApplicationId,

                    ApplicationNumber =
                        application.ApplicationNumber,

                    StudentName =
                        application.StudentName,

                    StudentDateOfBirth =
                        application.StudentDateOfBirth,

                    AcademicLevel =
                        application.AcademicLevel,

                    ExistingStudentProfileId =
                        application.ExistingStudentProfileId,

                    GuardianName =
                        application.GuardianName,

                    GuardianEmail =
                        application.GuardianEmail,

                    GuardianPhoneNumber =
                        application.GuardianPhoneNumber,

                    GuardianIdentificationLast4 =
                        application.GuardianIdentificationLast4,

                    ExistingParentProfileId =
                        application.ExistingParentProfileId,

                    RequestedStartDate =
                        application.RequestedStartDate,

                    HardcopyMaterialsRequested =
                        application.HardcopyMaterialsRequested,

                    FinalTwoWeeksConfirmed =
                        application.FinalTwoWeeksConfirmed,

                    ApplicantNotes =
                        application.ApplicantNotes,

                    RejectionReason =
                        application.RejectionReason,

                    Status =
                        application.Status,

                    SubmittedAt =
                        application.SubmittedAt,

                    ReviewedAt =
                        application.ReviewedAt,

                    ReviewedByName =
                        application.ReviewedByUser?.FullName ??
                        application.ReviewedByUser?.Email,

                    PotentialDuplicateStudent =
                        application.PotentialDuplicateStudent,

                    PotentialDuplicateGuardian =
                        application.PotentialDuplicateGuardian,

                    TotalMonthlyFee =
                        classItems.Sum(item =>
                            item.MonthlyFeeAtApplication),

                    SelectedClasses =
                        classItems,

                    StatusHistories =
                        histories
                };

            return View(viewModel);
        }

        /*
         * =====================================================
         * APPROVE APPLICATION
         * =====================================================
         */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            ApproveEnrollmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "The enrollment application could not " +
                    "be approved.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = model.EnrollmentApplicationId
                    });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var application =
                    await _context.EnrollmentApplications
                        .Include(item =>
                            item.SelectedSubjects)
                        .FirstOrDefaultAsync(item =>
                            item.EnrollmentApplicationId ==
                            model.EnrollmentApplicationId);

                if (application == null)
                {
                    return NotFound();
                }

                if (application.Status !=
                        EnrollmentStatus.PendingReview &&
                    application.Status !=
                        EnrollmentStatus.DuplicateReview)
                {
                    TempData["ErrorMessage"] =
                        "Only pending applications can be approved.";

                    return RedirectToAction(
                        nameof(Details),
                        new
                        {
                            id = model.EnrollmentApplicationId
                        });
                }

                if (application.SelectedSubjects.Count == 0)
                {
                    TempData["ErrorMessage"] =
                        "This application does not contain any " +
                        "selected classes.";

                    return RedirectToAction(
                        nameof(Details),
                        new
                        {
                            id = model.EnrollmentApplicationId
                        });
                }

                foreach (var selected in
                         application.SelectedSubjects)
                {
                    var classSession =
                        await _context.ClassSessions
                            .AsNoTracking()
                            .FirstOrDefaultAsync(session =>
                                session.ClassSessionId ==
                                selected.ClassSessionId);

                    if (classSession == null)
                    {
                        TempData["ErrorMessage"] =
                            "One of the selected classes " +
                            "could not be found.";

                        return RedirectToAction(
                            nameof(Details),
                            new
                            {
                                id =
                                    model
                                        .EnrollmentApplicationId
                            });
                    }

                    if (classSession.Status !=
                        ClassSessionStatus.Active)
                    {
                        TempData["ErrorMessage"] =
                            $"Class {GetClassDisplayName(classSession)} " +
                            "is no longer active.";

                        return RedirectToAction(
                            nameof(Details),
                            new
                            {
                                id =
                                    model
                                        .EnrollmentApplicationId
                            });
                    }

                    int occupiedSeats =
                        await _context.StudentEnrollments
                            .CountAsync(enrollment =>
                                enrollment.ClassSessionId ==
                                classSession.ClassSessionId &&
                                (enrollment.Status ==
                                    StudentEnrollmentStatus.Active ||
                                 enrollment.Status ==
                                    StudentEnrollmentStatus
                                        .PendingDrop));

                    if (occupiedSeats >=
                        classSession.Capacity)
                    {
                        TempData["ErrorMessage"] =
                            $"Class {GetClassDisplayName(classSession)} " +
                            "is already full.";

                        return RedirectToAction(
                            nameof(Details),
                            new
                            {
                                id =
                                    model
                                        .EnrollmentApplicationId
                            });
                    }

                    if (application.ExistingStudentProfileId
                        .HasValue)
                    {
                        bool alreadyEnrolled =
                            await _context.StudentEnrollments
                                .AnyAsync(enrollment =>
                                    enrollment.StudentProfileId ==
                                    application
                                        .ExistingStudentProfileId
                                        .Value &&
                                    enrollment.ClassSessionId ==
                                    classSession.ClassSessionId &&
                                    (enrollment.Status ==
                                        StudentEnrollmentStatus.Active ||
                                     enrollment.Status ==
                                        StudentEnrollmentStatus
                                            .PendingDrop));

                        if (alreadyEnrolled)
                        {
                            TempData["ErrorMessage"] =
                                $"{application.StudentName} is " +
                                "already enrolled in class " +
                                $"{GetClassDisplayName(classSession)}.";

                            return RedirectToAction(
                                nameof(Details),
                                new
                                {
                                    id =
                                        model
                                            .EnrollmentApplicationId
                                });
                        }
                    }
                }

                var previousStatus =
                    application.Status;

                var adminUserId =
                    _userManager.GetUserId(User);

                application.Status =
                    EnrollmentStatus.ApprovedPendingPayment;

                application.ReviewedAt =
                    DateTime.UtcNow;

                application.ReviewedByUserId =
                    adminUserId;

                application.RejectionReason =
                    null;

                _context.EnrollmentStatusHistories.Add(
                    new EnrollmentStatusHistory
                    {
                        EnrollmentApplicationId =
                            application.EnrollmentApplicationId,

                        PreviousStatus =
                            previousStatus,

                        NewStatus =
                            EnrollmentStatus
                                .ApprovedPendingPayment,

                        ChangedAt =
                            DateTime.UtcNow,

                        ChangedByUserId =
                            adminUserId,

                        Remarks =
                            string.IsNullOrWhiteSpace(
                                model.Remarks)
                                ? "Enrollment application approved."
                                : model.Remarks.Trim()
                    });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Application {application.ApplicationNumber} " +
                    "was approved and is pending payment.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = application.EnrollmentApplicationId
                    });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "An unexpected error occurred while approving " +
                    "the application.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = model.EnrollmentApplicationId
                    });
            }
        }

        /*
         * =====================================================
         * REJECT APPLICATION
         * =====================================================
         */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            RejectEnrollmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Please provide a valid rejection reason.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = model.EnrollmentApplicationId
                    });
            }

            var application =
                await _context.EnrollmentApplications
                    .FirstOrDefaultAsync(item =>
                        item.EnrollmentApplicationId ==
                        model.EnrollmentApplicationId);

            if (application == null)
            {
                return NotFound();
            }

            if (application.Status !=
                    EnrollmentStatus.PendingReview &&
                application.Status !=
                    EnrollmentStatus.DuplicateReview)
            {
                TempData["ErrorMessage"] =
                    "Only pending applications can be rejected.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = application.EnrollmentApplicationId
                    });
            }

            var previousStatus =
                application.Status;

            var adminUserId =
                _userManager.GetUserId(User);

            application.Status =
                EnrollmentStatus.Rejected;

            application.RejectionReason =
                model.RejectionReason.Trim();

            application.ReviewedAt =
                DateTime.UtcNow;

            application.ReviewedByUserId =
                adminUserId;

            _context.EnrollmentStatusHistories.Add(
                new EnrollmentStatusHistory
                {
                    EnrollmentApplicationId =
                        application.EnrollmentApplicationId,

                    PreviousStatus =
                        previousStatus,

                    NewStatus =
                        EnrollmentStatus.Rejected,

                    ChangedAt =
                        DateTime.UtcNow,

                    ChangedByUserId =
                        adminUserId,

                    Remarks =
                        model.RejectionReason.Trim()
                });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Application {application.ApplicationNumber} " +
                "was rejected.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = application.EnrollmentApplicationId
                });
        }

        /*
         * =====================================================
         * CANCEL APPLICATION
         * =====================================================
         */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(
            CancelEnrollmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Please provide a valid cancellation reason.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = model.EnrollmentApplicationId
                    });
            }

            var application =
                await _context.EnrollmentApplications
                    .FirstOrDefaultAsync(item =>
                        item.EnrollmentApplicationId ==
                        model.EnrollmentApplicationId);

            if (application == null)
            {
                return NotFound();
            }

            if (application.Status ==
                    EnrollmentStatus.Enrolled ||
                application.Status ==
                    EnrollmentStatus.Rejected ||
                application.Status ==
                    EnrollmentStatus.Cancelled)
            {
                TempData["ErrorMessage"] =
                    "This application can no longer be cancelled.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = application.EnrollmentApplicationId
                    });
            }

            var previousStatus =
                application.Status;

            var adminUserId =
                _userManager.GetUserId(User);

            application.Status =
                EnrollmentStatus.Cancelled;

            application.ReviewedAt =
                DateTime.UtcNow;

            application.ReviewedByUserId =
                adminUserId;

            _context.EnrollmentStatusHistories.Add(
                new EnrollmentStatusHistory
                {
                    EnrollmentApplicationId =
                        application.EnrollmentApplicationId,

                    PreviousStatus =
                        previousStatus,

                    NewStatus =
                        EnrollmentStatus.Cancelled,

                    ChangedAt =
                        DateTime.UtcNow,

                    ChangedByUserId =
                        adminUserId,

                    Remarks =
                        model.CancellationReason.Trim()
                });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Application {application.ApplicationNumber} " +
                "was cancelled.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = application.EnrollmentApplicationId
                });
        }

        /*
         * =====================================================
         * PRIVATE HELPERS
         * =====================================================
         */

        private static IQueryable<EnrollmentApplication>
            ApplySorting(
                IQueryable<EnrollmentApplication> query,
                string sortBy,
                string sortDirection)
        {
            bool descending =
                sortDirection == "desc";

            return sortBy switch
            {
                "applicationNumber" => descending
                    ? query.OrderByDescending(application =>
                        application.ApplicationNumber)
                    : query.OrderBy(application =>
                        application.ApplicationNumber),

                "studentName" => descending
                    ? query.OrderByDescending(application =>
                        application.StudentName)
                    : query.OrderBy(application =>
                        application.StudentName),

                "guardianName" => descending
                    ? query.OrderByDescending(application =>
                        application.GuardianName)
                    : query.OrderBy(application =>
                        application.GuardianName),

                "status" => descending
                    ? query.OrderByDescending(application =>
                        application.Status)
                    : query.OrderBy(application =>
                        application.Status),

                "totalFee" => descending
                    ? query.OrderByDescending(application =>
                        application.SelectedSubjects.Sum(
                            selected =>
                                (decimal?)
                                selected.MonthlyFeeAtApplication)
                        ?? 0)
                    : query.OrderBy(application =>
                        application.SelectedSubjects.Sum(
                            selected =>
                                (decimal?)
                                selected.MonthlyFeeAtApplication)
                        ?? 0),

                _ => descending
                    ? query.OrderByDescending(application =>
                        application.SubmittedAt)
                    : query.OrderBy(application =>
                        application.SubmittedAt)
            };
        }

        private static string NormalizeSortBy(
            string? sortBy)
        {
            string[] allowedValues =
            {
                "submittedAt",
                "applicationNumber",
                "studentName",
                "guardianName",
                "status",
                "totalFee"
            };

            return allowedValues.Contains(sortBy)
                ? sortBy!
                : "submittedAt";
        }

        private static string NormalizeSortDirection(
            string? sortDirection)
        {
            return string.Equals(
                sortDirection,
                "asc",
                StringComparison.OrdinalIgnoreCase)
                ? "asc"
                : "desc";
        }

        private static string GetClassDisplayName(
            ClassSession classSession)
        {
            return string.IsNullOrWhiteSpace(
                classSession.ClassName)
                ? classSession.ClassCode
                : classSession.ClassName;
        }
    }
}