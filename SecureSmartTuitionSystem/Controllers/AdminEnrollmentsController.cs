using SecureSmartTuitionSystem.Services.Communication;
using Stripe;
using Stripe.Checkout;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Security;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminEnrollmentsController : Controller
    {
        private readonly EnrollmentNoticeService _notices;
        private readonly ApplicationDbContext _context;
        private readonly StripeClient _stripeClient;
        private readonly SecurityEventService _securityEvents;

        private readonly UserManager<ApplicationUser>
            _userManager;

        private readonly IBillingService
            _billingService;

        private readonly IFirstPaymentEmailService
            _firstPaymentEmailService;

        private readonly ILogger<AdminEnrollmentsController>
            _logger;

        public AdminEnrollmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBillingService billingService,
            IFirstPaymentEmailService firstPaymentEmailService,
            ILogger<AdminEnrollmentsController> logger, SecurityEventService securityEvents, StripeClient stripeClient, EnrollmentNoticeService notices)
        {
            _context = context;
            _notices = notices;
            _stripeClient = stripeClient;
            _userManager = userManager;
            _billingService = billingService;

            _firstPaymentEmailService =
                firstPaymentEmailService;

            _logger = logger;
            _securityEvents = securityEvents;
        }

        /*
         * =====================================================
         * ENROLLMENT APPLICATION LIST
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> Index(
            EnrollmentStatus? status,
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

                    SortBy = sortBy,

                    SortDirection = sortDirection,

                    TotalApplicationCount =
                        totalApplicationCount,

                    PendingReviewCount =
                        pendingReviewCount,

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
                             ((enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date)) && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date))));

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

                    TotalMonthlyFee =
                        classItems.Sum(item =>
                            item.MonthlyFeeAtApplication),

                    SelectedClasses =
                        classItems,

                    StatusHistories =
                        histories
                };

            viewModel.DuplicateCandidates = await new StudentDuplicateReviewService(_context)
                .FindAsync(application.StudentName, application.StudentDateOfBirth,
                    application.StudentIdentificationNumber, application.ExistingStudentProfileId,
                    application.EnrollmentApplicationId, HttpContext.RequestAborted);
            viewModel.DuplicateReviewVersion = StudentDuplicateReviewService.ReviewVersion(viewModel.DuplicateCandidates);

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
                    EnrollmentStatus.PendingReview)
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

                // Prevent a bill/payment link from being issued for an
                // application that cannot safely be bound to the family.
                string? guardianLinkError = await new GuardianAccountLinkPolicy(_context)
                    .ValidateAsync(application, HttpContext.RequestAborted);
                if (guardianLinkError != null)
                {
                    TempData["ErrorMessage"] = guardianLinkError;
                    return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId });
                }

                if (application.SelectedSubjects.Count == 0)
                {
                    TempData["ErrorMessage"] =
                        "This application does not contain " +
                        "any selected classes.";

                    return RedirectToAction(
                        nameof(Details),
                        new
                        {
                            id = model.EnrollmentApplicationId
                        });
                }

                /*
                 * Recheck every selected class before approval.
                 */
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
                                    model.EnrollmentApplicationId
                            });
                    }

                    if (!AcademicYearPolicy.CanEnrollOn(classSession, application.RequestedStartDate))
                    {
                        TempData["ErrorMessage"] =
                            $"Class {GetClassDisplayName(classSession)} " +
                            "is unavailable for the requested enrollment start date.";

                        return RedirectToAction(
                            nameof(Details),
                            new
                            {
                                id =
                                    model.EnrollmentApplicationId
                            });
                    }

                    int occupiedSeats =
                        await new EnrollmentRulesService(_context).OccupiedSeatsAsync(classSession.ClassSessionId, application.EnrollmentApplicationId, startDate: application.RequestedStartDate);

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
                                    model.EnrollmentApplicationId
                            });
                    }

                    /*
                     * Only check existing enrollment when the
                     * application is linked to an existing student.
                     */
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
                                     (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date))));

                        if (alreadyEnrolled)
                        {
                            TempData["ErrorMessage"] =
                                $"{application.StudentName} is already " +
                                "enrolled in class " +
                                $"{GetClassDisplayName(classSession)}.";

                            return RedirectToAction(
                                nameof(Details),
                                new
                                {
                                    id =
                                        model.EnrollmentApplicationId
                                });
                        }
                    }
                }

                if (application.RequestedStartDate.Date < ClassScheduleService.Today)
                {
                    TempData["ErrorMessage"] = "The requested start date has passed. Reject this request and ask the student or parent to submit a new request with a future start date.";
                    return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId });
                }

                var ruleError = await new EnrollmentRulesService(_context).ValidateAsync(
                    application.SelectedSubjects.Select(x => x.ClassSessionId).ToList(), application.RequestedStartDate,
                    application.ExistingStudentProfileId, application.EnrollmentApplicationId, durationMonths: application.EnrollmentDurationMonths);
                if (ruleError != null) { TempData["ErrorMessage"] = ruleError; return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId }); }

                // Recompute within the approval transaction. Client-side checks
                // are convenience only; a direct POST must pass the same review.
                var duplicateCandidates = await new StudentDuplicateReviewService(_context)
                    .FindAsync(application.StudentName, application.StudentDateOfBirth,
                        application.StudentIdentificationNumber, application.ExistingStudentProfileId,
                        application.EnrollmentApplicationId, HttpContext.RequestAborted);
                if (!StudentDuplicateReviewService.IsReviewComplete(duplicateCandidates,
                    model.DuplicateReviewConfirmed, model.DuplicateReviewVersion, model.DuplicateReviewReason))
                {
                    TempData["ErrorMessage"] = "Review the current possible duplicate records, confirm that this is a different student, and provide a reason of 10 to 200 characters before approval. If it is the same student, reject this application and arrange the correct existing-student enrollment.";
                    return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId });
                }

                string approvalHistoryRemarks = string.IsNullOrWhiteSpace(model.Remarks)
                    ? "Enrollment application approved."
                    : model.Remarks.Trim();
                if (duplicateCandidates.Count > 0)
                {
                    string review = $"Duplicate review: {duplicateCandidates.Count} candidate(s); confirmed different student. Reason: {model.DuplicateReviewReason!.Trim()}";
                    // Preserve the mandatory review reason within the existing
                    // 500-character history column; optional remarks use the rest.
                    int available = 500 - review.Length - 3;
                    approvalHistoryRemarks = review + " | " + approvalHistoryRemarks[..Math.Min(available, approvalHistoryRemarks.Length)];
                }

                EnrollmentStatus previousStatus =
                    application.Status;

                string? adminUserId =
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

                        Remarks = approvalHistoryRemarks
                    });

                /*
                 * Generate the bill inside the same transaction.
                 *
                 * BillingService will reuse the current transaction.
                 */
                var bill =
                    await _billingService.GenerateBillAsync(
                        application.EnrollmentApplicationId);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                string paymentAccessToken =
                    bill.RawPaymentAccessToken
                    ?? PaymentAccessTokenService.GenerateToken();

                if (bill.RawPaymentAccessToken == null)
                {
                    bill.PaymentAccessTokenHash =
                        PaymentAccessTokenService.HashToken(
                            paymentAccessToken);

                    bill.PaymentAccessTokenExpiresAt =
                        DateTime.UtcNow.AddDays(7);

                    await _context.SaveChangesAsync();
                }

                string? paymentUrl = Url.Action(
                    "Checkout",
                    "Payments",
                    new
                    {
                        token = paymentAccessToken
                    },
                    Request.Scheme);

                bool paymentEmailSent = false;

                if (!string.IsNullOrWhiteSpace(paymentUrl))
                {
                    try
                    {
                        await _firstPaymentEmailService.SendAsync(
                            application.GuardianEmail,
                            application.GuardianName,
                            application.StudentName,
                            bill,
                            paymentUrl,
                            HttpContext.RequestAborted);

                        paymentEmailSent = true;
                    }
                    catch (Exception exception)
                    {
                        await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,
                            "An application operation failed. Refer to the private server log using the trace ID.");
                        _logger.LogError(
                            exception,
                            "First-payment email could not be sent " +
                            "for bill {BillNumber}.",
                            bill.BillNumber);
                    }
                }

                TempData["SuccessMessage"] =
                    $"Application {application.ApplicationNumber} " +
                    $"was approved. Bill {bill.BillNumber} " +
                    (paymentEmailSent
                        ? "was generated and the payment link was " +
                          "sent to the guardian email."
                        : "was generated, but the payment email " +
                          "could not be sent. Check the email settings.");

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id =
                            application.EnrollmentApplicationId
                    });
            }
            catch (InvalidOperationException exception)
            {
                await transaction.RollbackAsync();

                _logger.LogWarning(
                    exception,
                    "Enrollment approval was rejected for application {ApplicationId}.",
                    model.EnrollmentApplicationId);

                TempData["ErrorMessage"] =
                    "The application could not be approved in its current state.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = model.EnrollmentApplicationId
                    });
            }
            catch (DbUpdateException exception)
            {
                await transaction.RollbackAsync();

                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,

                    "An application operation failed. Refer to the private server log using the trace ID.");

                _logger.LogError(
                    exception,
                    "Database update failed while approving application {ApplicationId}.",
                    model.EnrollmentApplicationId);

                TempData["ErrorMessage"] =
                    "The application could not be approved because " +
                    "the database rejected the update.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = model.EnrollmentApplicationId
                    });
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,

                    "An application operation failed. Refer to the private server log using the trace ID.");

                _logger.LogError(
                    exception,
                    "Unexpected approval failure for application {ApplicationId}.",
                    model.EnrollmentApplicationId);

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

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var application =
                await _context.EnrollmentApplications
                    .Include(x => x.SelectedSubjects)
                    .FirstOrDefaultAsync(item =>
                        item.EnrollmentApplicationId ==
                        model.EnrollmentApplicationId);

            if (application == null)
            {
                return NotFound();
            }

            if (application.Status !=
                    EnrollmentStatus.PendingReview)
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

            await _notices.QueueAsync(application, rejected: true);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

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

            await using var cancelTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var application =
                await _context.EnrollmentApplications
                    .Include(item => item.SelectedSubjects)
                    .FirstOrDefaultAsync(item =>
                        item.EnrollmentApplicationId ==
                        model.EnrollmentApplicationId);

            if (application == null)
            {
                return NotFound();
            }

            if (application.Status !=
                EnrollmentStatus.ApprovedPendingPayment)
            {
                TempData["ErrorMessage"] =
                    "Only an application awaiting payment can be cancelled.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = application.EnrollmentApplicationId
                    });
            }

            var pendingPayments = await _context.Payments.Where(x => x.Bill.EnrollmentApplicationId == application.EnrollmentApplicationId && x.Status == PaymentStatus.Pending).ToListAsync();
            try
            {
                var sessions = new SessionService(_stripeClient);
                foreach (var payment in pendingPayments.Where(x => x.PaymentProvider == "Stripe" && x.ProviderCheckoutSessionId != null))
                {
                    var checkout = await sessions.GetAsync(payment.ProviderCheckoutSessionId!, cancellationToken: HttpContext.RequestAborted);
                    if (checkout.Status == "complete") { TempData["ErrorMessage"] = "A payment is already being confirmed. Wait for its result before cancelling."; return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId }); }
                    if (checkout.Status == "open") await sessions.ExpireAsync(checkout.Id, cancellationToken: HttpContext.RequestAborted);
                    else if (checkout.Status != "expired") { TempData["ErrorMessage"] = "The active checkout cannot currently be cancelled."; return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId }); }
                }
            }
            catch (StripeException ex) { _logger.LogError(ex, "Could not expire checkout during application cancellation."); TempData["ErrorMessage"] = "Could not stop the active payment checkout. The application has not been cancelled; retry after checking payment status."; return RedirectToAction(nameof(Details), new { id = application.EnrollmentApplicationId }); }
            foreach (var payment in pendingPayments) { payment.Status = PaymentStatus.Failed; payment.FailedAt = DateTime.UtcNow; payment.FailureReason = "Enrollment application cancelled by Admin."; }

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

            var unpaidBills = await _context.Bills.Where(b => b.EnrollmentApplicationId == application.EnrollmentApplicationId && (b.Status == BillStatus.PendingPayment || b.Status == BillStatus.Overdue)).ToListAsync();
            foreach (var bill in unpaidBills) { bill.Status = BillStatus.Cancelled; bill.CancelledAt = DateTime.UtcNow; bill.CancellationReason = model.CancellationReason.Trim(); bill.PaymentAccessTokenHash = null; bill.PaymentAccessTokenExpiresAt = null; }
            await _notices.QueueAsync(application, rejected: true, cancellationReason: model.CancellationReason.Trim());
            await _context.SaveChangesAsync();
            await cancelTransaction.CommitAsync();

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
                ? "Class"
                : classSession.ClassName;
        }
    }
}
