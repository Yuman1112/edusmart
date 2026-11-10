using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Models.Security;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.ViewModels.Billing;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.Services.Security;
using Stripe;
using Stripe.Checkout;
using System.Globalization;

namespace SecureSmartTuitionSystem.Controllers
{
    public class PaymentsController : Controller
    {
        private const string VerifiedReceiptSessionKey =
            "Payments.VerifiedReceiptSessionId";

        private readonly ApplicationDbContext _context;
        private readonly SecurityEventService _securityEvents;
        private readonly PaymentCallbackTokenService _callbackTokens;
        private readonly StripeClient _stripeClient;

        private readonly IPaymentCompletionService
            _paymentCompletionService;

        private readonly SignInManager<ApplicationUser>
            _signInManager;

        private readonly ILogger<PaymentsController>
            _logger;

        public PaymentsController(
            ApplicationDbContext context,
            StripeClient stripeClient,
            IPaymentCompletionService paymentCompletionService,
            SignInManager<ApplicationUser> signInManager,
            ILogger<PaymentsController> logger, SecurityEventService securityEvents, PaymentCallbackTokenService callbackTokens)
        {
            _context = context;
            _stripeClient = stripeClient;
            _paymentCompletionService = paymentCompletionService;
            _signInManager = signInManager;
            _logger = logger;
            _securityEvents = securityEvents;
            _callbackTokens = callbackTokens;
        }

        /*
         * First-time payment uses the anonymous secure link.
         * After account activation, only the linked parent
         * may open a bill for payment from the portal.
         */
        [Authorize(Roles = "Parent")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenBill(
            int id)
        {
            string? currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                return Challenge();
            }

            var bill =
                await _context.Bills
                    .Include(item =>
                        item.EnrollmentApplication)
                    .ThenInclude(application =>
                        application.ExistingParentProfile)
                    .FirstOrDefaultAsync(item =>
                        item.BillId == id &&
                        item.EnrollmentApplication
                            .ExistingParentProfile != null &&
                        item.EnrollmentApplication
                            .ExistingParentProfile
                            .ApplicationUserId ==
                        currentUserId);

            if (bill == null)
            {
                return NotFound();
            }

            bool hasSuccessfulPayment =
                await _context.Payments
                    .AsNoTracking()
                    .AnyAsync(payment =>
                        payment.BillId == bill.BillId &&
                        payment.Status == PaymentStatus.Succeeded);

            if (bill.Status == BillStatus.Paid ||
                hasSuccessfulPayment)
            {
                TempData["SuccessMessage"] =
                    "This bill has already been paid.";

                return RedirectToAction(
                    "Details",
                    "Billing",
                    new { id = bill.BillId });
            }

            if (bill.Status !=
                    BillStatus.PendingPayment &&
                bill.Status !=
                    BillStatus.Overdue)
            {
                TempData["ErrorMessage"] =
                    "This bill is not available for payment.";

                return RedirectToAction(
                    "Details",
                    "Billing",
                    new
                    {
                        id = bill.BillId
                    });
            }

            string paymentAccessToken =
                await GenerateUniqueAccessTokenAsync();

            bill.PaymentAccessTokenHash =
                PaymentAccessTokenService.HashToken(
                    paymentAccessToken);

            bill.PaymentAccessTokenExpiresAt =
                DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Checkout),
                new
                {
                    token = paymentAccessToken,
                    preserveSession = true
                });
        }

        /*
         * This page supports applicants who do not yet
         * have a system account.
         *
         * Access is protected using a secure random token.
         */
        [AllowAnonymous]
        [EnableRateLimiting("PaymentPolicy")]
        [HttpGet]
        public async Task<IActionResult> Checkout(
            string? token,
            bool preserveSession = false)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return NotFound();
            }

            string tokenHash =
                PaymentAccessTokenService.HashToken(token);

            var bill =
                await _context.Bills
                    .AsNoTracking()
                    .Include(item =>
                        item.EnrollmentApplication)
                    .ThenInclude(application => application.SelectedSubjects)
                    .ThenInclude(selected => selected.ClassSession)
                    .Include(item =>
                        item.Items)
                    .FirstOrDefaultAsync(item =>
                        item.PaymentAccessTokenHash == tokenHash &&
                        item.PaymentAccessTokenExpiresAt.HasValue &&
                        item.PaymentAccessTokenExpiresAt.Value >
                            DateTime.UtcNow);

            if (bill == null)
            {
                return NotFound();
            }

            if (!preserveSession &&
                User.Identity?.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession = false
                    });
            }

            bool hasSuccessfulPayment =
                await _context.Payments
                    .AsNoTracking()
                    .AnyAsync(payment =>
                        payment.BillId == bill.BillId &&
                        payment.Status == PaymentStatus.Succeeded);

            if (bill.Status == BillStatus.Paid ||
                hasSuccessfulPayment)
            {
                TempData["SuccessMessage"] =
                    "This bill has already been paid.";
            }



            var application =
                bill.EnrollmentApplication;

            var viewModel =
                new PaymentCheckoutViewModel
                {
                    PaymentAccessToken =
                        token,

                    PreserveSession =
                        preserveSession,

                    BillNumber =
                        bill.BillNumber,

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

                    EnrollmentDurationMonths =
                        application.EnrollmentDurationMonths,

                    TuitionSubtotal =
                        bill.TuitionSubtotal,

                    TuitionDiscountAmount =
                        bill.TuitionDiscountAmount,

                    HardcopyFee =
                        bill.HardcopyFee,

                    DeliveryFee =
                        bill.DeliveryFee,

                    TotalAmount =
                        bill.TotalAmount,

                    Currency =
                        bill.Currency,

                    Status =
                        bill.Status,

                    IssuedAt =
                        bill.IssuedAt,

                    DueAt =
                        bill.DueAt,

                    Items =
                        bill.Items
                            .OrderBy(item =>
                                item.BillItemId)
                            .Select(item =>
                                new PaymentCheckoutItemViewModel
                                {
                                    Description =
                                        item.Description,

                                    ClassNames = item.ItemType == BillItemType.Tuition
                                        ? bill.EnrollmentApplication.SelectedSubjects
                                            .OrderBy(selected => selected.ClassSessionId)
                                            .Select(selected => selected.ClassSession.ClassName)
                                            .Where(name => !string.IsNullOrWhiteSpace(name))
                                            .Select(name => name!)
                                            .ToList()
                                        : new List<string>(),

                                    Quantity =
                                        item.Quantity,

                                    UnitAmount =
                                        item.UnitAmount,

                                    LineTotal =
                                        item.LineTotal
                                })
                            .ToList()
                };

            return View(viewModel);
        }

        /*
         * Creates a Stripe Test Checkout Session.
         */
        [AllowAnonymous]
        [EnableRateLimiting("PaymentPolicy")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            CreateCheckoutSession(
                string token,
                bool preserveSession = false)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return NotFound();
            }

            string tokenHash =
                PaymentAccessTokenService.HashToken(token);

            var bill =
                await _context.Bills
                    .Include(item =>
                        item.EnrollmentApplication)
                    .FirstOrDefaultAsync(item =>
                        item.PaymentAccessTokenHash == tokenHash &&
                        item.PaymentAccessTokenExpiresAt.HasValue &&
                        item.PaymentAccessTokenExpiresAt.Value >
                            DateTime.UtcNow);

            if (bill == null)
            {
                return NotFound();
            }

            bool hasSuccessfulPayment =
                await _context.Payments
                    .AsNoTracking()
                    .AnyAsync(payment =>
                        payment.BillId == bill.BillId &&
                        payment.Status == PaymentStatus.Succeeded);

            if (bill.Status == BillStatus.Paid ||
                hasSuccessfulPayment)
            {
                TempData["SuccessMessage"] =
                    "This bill has already been paid.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession
                    });
            }

            if (bill.Status !=
                    BillStatus.PendingPayment &&
                bill.Status !=
                    BillStatus.Overdue)
            {
                TempData["ErrorMessage"] =
                    "This bill is not available for payment.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession
                    });
            }

            string? academicError = await ValidateAcademicBill(bill);
            if (academicError != null) { TempData["ErrorMessage"] = academicError + " Ask Admin to review this application before payment."; return RedirectToAction(nameof(Checkout), new { token, preserveSession }); }

            if (bill.TotalAmount == 0m)
            {
                try { await _paymentCompletionService.CompleteZeroBillAsync(bill.BillId, HttpContext.RequestAborted); return View("FreeCompleted"); }
                catch (InvalidOperationException) { TempData["ErrorMessage"] = "This free enrollment cannot be completed. Ask Admin to review its class availability."; return RedirectToAction(nameof(Checkout), new { token, preserveSession }); }
            }

            if (bill.TotalAmount <= 0)
            {
                TempData["ErrorMessage"] =
                    "The bill amount must be greater than RM0.00.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession
                    });
            }

            long amountInSen;

            try
            {
                amountInSen =
                    checked(
                        (long)decimal.Round(
                            bill.TotalAmount * 100m,
                            0,
                            MidpointRounding.AwayFromZero));
            }
            catch (OverflowException)
            {
                TempData["ErrorMessage"] =
                    "The bill amount is invalid.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            Payment? payment = null;

            try
            {
                // Recheck inside the serializable transaction; repeated clicks reuse the open Checkout.
                await _context.Entry(bill).ReloadAsync();
                await _context.Entry(bill.EnrollmentApplication).ReloadAsync();
                if ((bill.Status != BillStatus.PendingPayment && bill.Status != BillStatus.Overdue) || bill.EnrollmentApplication.Status != SecureSmartTuitionSystem.Models.Enrollment.EnrollmentStatus.ApprovedPendingPayment)
                    return NotFound();
                academicError = await ValidateAcademicBill(bill);
                if (academicError != null) { TempData["ErrorMessage"] = academicError; return RedirectToAction(nameof(Checkout), new { token, preserveSession }); }
                var pendingCheckout = await _context.Payments.Where(x => x.BillId == bill.BillId && x.PaymentProvider == "Stripe" && x.Status == PaymentStatus.Pending && x.ProviderCheckoutSessionId != null)
                    .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
                if (pendingCheckout != null)
                {
                    var existingSession = await new SessionService(_stripeClient).GetAsync(pendingCheckout.ProviderCheckoutSessionId!);
                    if (existingSession.Status == "open" && !string.IsNullOrWhiteSpace(existingSession.Url)) { await transaction.CommitAsync(); return Redirect(existingSession.Url); }
                    if (existingSession.Status == "complete") { TempData["ErrorMessage"] = "Payment confirmation is being processed. Do not start another payment."; return RedirectToAction(nameof(Checkout), new { token, preserveSession }); }
                    if (existingSession.Status != "expired") return BadRequest();
                    pendingCheckout.Status = PaymentStatus.Failed; pendingCheckout.FailedAt = DateTime.UtcNow; pendingCheckout.FailureReason = "Checkout session expired before payment.";
                }

                string paymentNumber =
                    await GenerateNextPaymentNumberAsync();

                payment =
                    new Payment
                    {
                        PaymentNumber =
                            paymentNumber,

                        BillId =
                            bill.BillId,

                        Amount =
                            bill.TotalAmount,

                        Currency =
                            bill.Currency,

                        PaymentProvider =
                            "Stripe",

                        Status =
                            PaymentStatus.Pending,

                        CreatedAt =
                            DateTime.UtcNow
                    };

                _context.Payments.Add(payment);

                await _context.SaveChangesAsync();

                string baseUrl =
                    $"{Request.Scheme}://{Request.Host}";

                var options =
                    new SessionCreateOptions
                    {
                        Mode = "payment",

                        SuccessUrl =
                            $"{baseUrl}/Payments/Success" +
                            "?session_id={CHECKOUT_SESSION_ID}" +
                            $"&callbackToken={Uri.EscapeDataString(_callbackTokens.Create(payment.PaymentId))}",

                        CancelUrl =
                            $"{baseUrl}/Payments/Cancelled" +
                            $"?token={Uri.EscapeDataString(token)}" +
                            $"&preserveSession={preserveSession.ToString().ToLowerInvariant()}",

                        CustomerEmail =
                            bill.EnrollmentApplication
                                .GuardianEmail,

                        ClientReferenceId =
                            payment.PaymentNumber,

                        LineItems =
                            new List<SessionLineItemOptions>
                            {
                                new()
                                {
                                    Quantity = 1,

                                    PriceData =
                                        new SessionLineItemPriceDataOptions
                                        {
                                            Currency = "myr",

                                            UnitAmount =
                                                amountInSen,

                                            ProductData =
                                                new SessionLineItemPriceDataProductDataOptions
                                                {
                                                    Name =
                                                        $"EduSmart Bill " +
                                                        $"{bill.BillNumber}",

                                                    Description =
                                                        $"Enrollment " +
                                                        $"{bill.EnrollmentApplication.ApplicationNumber} " +
                                                        $"for {bill.EnrollmentApplication.StudentName}"
                                                }
                                        }
                                }
                            },

                        Metadata =
                            new Dictionary<string, string>
                            {
                                ["paymentId"] =
                                    payment.PaymentId.ToString(),

                                ["paymentNumber"] =
                                    payment.PaymentNumber,

                                ["billId"] =
                                    bill.BillId.ToString(),

                                ["billNumber"] =
                                    bill.BillNumber
                            },

                        PaymentIntentData =
                            new SessionPaymentIntentDataOptions
                            {
                                Metadata =
                                    new Dictionary<string, string>
                                    {
                                        ["paymentId"] =
                                            payment.PaymentId.ToString(),

                                        ["paymentNumber"] =
                                            payment.PaymentNumber,

                                        ["billId"] =
                                            bill.BillId.ToString()
                                    }
                            }
                    };

                var requestOptions =
                    new RequestOptions
                    {
                        IdempotencyKey =
                            $"edusmart-checkout-" +
                            $"{bill.BillId}-" +
                            $"{payment.PaymentId}-" +
                            $"{bill.IssuedAt.Ticks}"
                    };

                var sessionService =
                    new SessionService(
                        _stripeClient);

                Session session =
                    await sessionService.CreateAsync(
                        options,
                        requestOptions);

                if (string.IsNullOrWhiteSpace(
                        session.Url))
                {
                    throw new InvalidOperationException(
                        "Stripe did not return a Checkout URL.");
                }

                payment.ProviderCheckoutSessionId =
                    session.Id;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Redirect(session.Url);
            }
            catch (StripeException exception)
            {
                await transaction.RollbackAsync();

                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,

                    "An application operation failed. Refer to the private server log using the trace ID.");

                _logger.LogError(
                    exception,
                    "Stripe Checkout creation failed for bill {BillId}.",
                    bill.BillId);

                await MarkPaymentFailedAsync(
                    payment,
                    "Stripe Checkout could not be created.");

                TempData["ErrorMessage"] =
                    "The payment page could not be opened. " +
                    "Please try again.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession
                    });
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,

                    "An application operation failed. Refer to the private server log using the trace ID.");

                _logger.LogError(
                    exception,
                    "Payment creation failed for bill {BillId}.",
                    bill.BillId);

                await MarkPaymentFailedAsync(
                    payment,
                    "An unexpected payment error occurred.");

                TempData["ErrorMessage"] =
                    "An unexpected error occurred. Please try again.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token,
                        preserveSession
                    });
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("PaymentPolicy")]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Success([FromQuery(Name = "session_id")] string? sessionId,
            string? callbackToken)
        {
            if (!_callbackTokens.TryRead(callbackToken, out int paymentId) ||
                string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 255 ||
                !await _context.Payments.AsNoTracking().AnyAsync(x => x.PaymentId == paymentId &&
                    x.ProviderCheckoutSessionId == sessionId))
                return BadRequest("The payment verification link is invalid or expired.");
            return View("VerifyPayment", new PaymentVerificationViewModel {
                SessionId = sessionId, CallbackToken = callbackToken!
            });
        }

        /*
         * Reconcile only through a protected POST after validating the callback token.
         * Stripe's server response and the local amount/currency checks determine success.
         */
        [AllowAnonymous]
        [EnableRateLimiting("PaymentPolicy")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ConfirmPayment(string? sessionId, string? callbackToken,
            CancellationToken cancellationToken)
        {
            if (!_callbackTokens.TryRead(callbackToken, out int expectedPaymentId) ||
                string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 255 ||
                !await _context.Payments.AsNoTracking().AnyAsync(x => x.PaymentId == expectedPaymentId &&
                    x.ProviderCheckoutSessionId == sessionId, cancellationToken))
                return BadRequest("The payment verification link is invalid or expired.");
            ViewBag.PaymentVerified = false;
            ViewBag.BillId = null;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                ViewBag.StatusMessage =
                    "The Stripe Checkout Session ID is missing.";

                return View("Success");
            }

            try
            {
                var sessionService =
                    new SessionService(_stripeClient);

                Session session =
                    await sessionService.GetAsync(
                        sessionId,
                        options: null,
                        requestOptions: null,
                        cancellationToken:
                            cancellationToken);

                bool isPaid =
                    string.Equals(
                        session.PaymentStatus,
                        "paid",
                        StringComparison.OrdinalIgnoreCase);

                if (!isPaid)
                {
                    ViewBag.StatusMessage =
                        "Stripe has not confirmed this payment yet.";

                    return View("Success");
                }

                if (!session.AmountTotal.HasValue ||
                    session.AmountTotal.Value <= 0 ||
                    string.IsNullOrWhiteSpace(session.Currency))
                {
                    ViewBag.StatusMessage =
                        "Stripe returned incomplete payment information.";

                    return View("Success");
                }

                PaymentCompletionResult result =
                    await _paymentCompletionService
                        .CompleteStripePaymentAsync(
                            checkoutSessionId:
                                session.Id,

                            paymentIntentId:
                                session.PaymentIntentId,

                            stripeEventId:
                                $"success-page-{session.Id}",

                            amountPaidInSen:
                                session.AmountTotal.Value,

                            currency:
                                session.Currency,

                            cancellationToken:
                                cancellationToken);

                ViewBag.PaymentVerified = true;

                ViewBag.StatusMessage =
                    "Payment was confirmed. Bill is now paid.";

                ViewBag.CheckoutSessionId =
                    session.Id;

                HttpContext.Session.SetString(
                    VerifiedReceiptSessionKey,
                    session.Id);

                _logger.LogInformation(
                    "Stripe Checkout Session {SessionId} " +
                    "was reconciled successfully for bill {BillNumber}.",
                    session.Id,
                    result.BillNumber);
            }
            catch (StripeException exception)
            {
                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,
                    "An application operation failed. Refer to the private server log using the trace ID.");
                _logger.LogError(
                    exception,
                    "Stripe verification failed for " +
                    "Checkout Session {SessionId}.",
                    sessionId);

                ViewBag.StatusMessage =
                    "Stripe could not verify this payment.";
            }
            catch (InvalidOperationException exception)
            {
                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,
                    "An application operation failed. Refer to the private server log using the trace ID.");
                _logger.LogError(
                    exception,
                    "Local payment completion failed for " +
                    "Checkout Session {SessionId}.",
                    sessionId);

                ViewBag.StatusMessage =
                    "The payment could not be verified. " +
                    "Please contact support.";
            }
            catch (Exception exception)
            {
                await _securityEvents.RecordAsync(HttpContext, "OperationError", SecuritySeverity.High,
                    "An application operation failed. Refer to the private server log using the trace ID.");
                _logger.LogError(
                    exception,
                    "Unexpected payment completion error for " +
                    "Checkout Session {SessionId}.",
                    sessionId);

                ViewBag.StatusMessage =
                    "The payment was received, but EduSmart could " +
                    "not update the local payment record.";
            }

            return View("Success");
        }

        [Authorize(Roles = "Admin,Parent,Student")]
        [HttpGet]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Receipt(
            int id,
            CancellationToken cancellationToken)
        {
            string? currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            IQueryable<Payment> query =
                _context.Payments
                    .AsNoTracking()
                    .Include(payment =>
                        payment.Bill)
                    .ThenInclude(bill =>
                        bill.Items)
                    .Include(payment =>
                        payment.Bill)
                    .ThenInclude(bill =>
                        bill.EnrollmentApplication)
                    .ThenInclude(application =>
                        application.ExistingParentProfile)
                    .Include(payment =>
                        payment.Bill)
                    .ThenInclude(bill =>
                        bill.EnrollmentApplication)
                    .ThenInclude(application =>
                        application.ExistingStudentProfile);

            /*
             * Parent may only view receipts that belong
             * to the linked Parent Profile.
             */
            if (User.IsInRole("Parent"))
            {
                query =
                    query.Where(payment =>
                        payment.Bill
                            .EnrollmentApplication
                            .ExistingParentProfile != null &&

                        payment.Bill
                            .EnrollmentApplication
                            .ExistingParentProfile!
                            .ApplicationUserId ==
                        currentUserId);
            }

            /*
             * Student may only view receipts that belong
             * to the linked Student Profile.
             */
            else if (User.IsInRole("Student"))
            {
                query =
                    query.Where(payment =>
                        payment.Bill
                            .EnrollmentApplication
                            .ExistingStudentProfile != null &&

                        payment.Bill
                            .EnrollmentApplication
                            .ExistingStudentProfile!
                            .ApplicationUserId ==
                        currentUserId);
            }

            /*
             * Admin may view every receipt.
             */
            else if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            Payment? payment =
                await query.FirstOrDefaultAsync(
                    item =>
                        item.PaymentId == id,
                    cancellationToken);

            if (payment == null)
            {
                return NotFound();
            }

            /*
             * A receipt only exists after successful payment.
             */
            if (payment.Status !=
                    PaymentStatus.Succeeded ||
                payment.Bill.Status !=
                    BillStatus.Paid ||
                !payment.CompletedAt.HasValue)
            {
                return NotFound();
            }

            PaymentReceiptViewModel viewModel =
                await BuildReceiptViewModelAsync(payment);

            return View(
                "Receipt",
                viewModel);
        }


        /*
         * =====================================================
         * FIRST-PAYMENT ANONYMOUS RECEIPT
         * =====================================================
         */

        [AllowAnonymous]
        [EnableRateLimiting("PaymentPolicy")]
        [HttpGet]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ReceiptBySession(
            string? sessionId,
            CancellationToken cancellationToken)
        {
            /*
             * Stripe Checkout Session IDs begin with cs_.
             */
            if (string.IsNullOrWhiteSpace(sessionId) ||
                !sessionId.StartsWith(
                    "cs_",
                    StringComparison.Ordinal))
            {
                return NotFound();
            }

            string? verifiedSessionId =
                HttpContext.Session.GetString(
                    VerifiedReceiptSessionKey);

            if (!string.Equals(
                    verifiedSessionId,
                    sessionId,
                    StringComparison.Ordinal))
            {
                return NotFound();
            }

            Payment? payment =
                await _context.Payments
                    .AsNoTracking()
                    .Include(item =>
                        item.Bill)
                    .ThenInclude(bill =>
                        bill.Items)
                    .Include(item =>
                        item.Bill)
                    .ThenInclude(bill =>
                        bill.EnrollmentApplication)
                    .FirstOrDefaultAsync(
                        item =>
                            item.ProviderCheckoutSessionId ==
                                sessionId &&

                            item.Status ==
                                PaymentStatus.Succeeded &&

                            item.Bill.Status ==
                                BillStatus.Paid,
                        cancellationToken);

            if (payment == null ||
                !payment.CompletedAt.HasValue)
            {
                return NotFound();
            }

            PaymentReceiptViewModel viewModel =
                await BuildReceiptViewModelAsync(payment);

            return View(
                "Receipt",
                viewModel);
        }


        /*
         * =====================================================
         * BUILD RECEIPT VIEW MODEL
         * =====================================================
         */

        private async Task<PaymentReceiptViewModel>
            BuildReceiptViewModelAsync(
                Payment payment)
        {
            Bill bill =
                payment.Bill;

            var application =
                bill.EnrollmentApplication;

            DateTime tuitionStartMonth =
                new DateTime(
                    application.RequestedStartDate.Year,
                    application.RequestedStartDate.Month,
                    1);

            int durationMonths =
                Math.Max(
                    application.EnrollmentDurationMonths,
                    1);

            DateTime tuitionEndMonth =
                tuitionStartMonth.AddMonths(
                    durationMonths - 1);

            string tuitionPeriod;

            if (durationMonths == 1)
            {
                tuitionPeriod =
                    tuitionStartMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture);
            }
            else
            {
                string startMonthText =
                    tuitionStartMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture);

                string endMonthText =
                    tuitionEndMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture);

                tuitionPeriod =
                    $"{startMonthText} to {endMonthText}";
            }

            var receiptClassNames = await _context.Set<SecureSmartTuitionSystem.Models.Enrollment.EnrollmentApplicationSubject>()
                .AsNoTracking().Where(selected => selected.EnrollmentApplicationId == application.EnrollmentApplicationId)
                .OrderBy(selected => selected.ClassSessionId)
                .Select(selected => selected.ClassSession.ClassName ?? selected.ClassSession.ClassCode)
                .ToListAsync(HttpContext.RequestAborted);

            return new PaymentReceiptViewModel
            {
                DownloadFileName = ReceiptFileNaming.ForRange(tuitionStartMonth, tuitionEndMonth),
                PaymentId =
                    payment.PaymentId,

                PaymentNumber =
                    payment.PaymentNumber,

                BillNumber =
                    bill.BillNumber,

                ApplicationNumber =
                    application.ApplicationNumber,

                StudentName =
                    application.StudentName,

                TuitionPeriod =
                    tuitionPeriod,

                GuardianName =
                    application.GuardianName,

                GuardianEmail =
                    application.GuardianEmail,

                PaymentProvider =
                    payment.PaymentProvider,

                Currency =
                    payment.Currency,

                AmountPaid =
                    payment.Amount,

                CompletedAt =
                    payment.CompletedAt!.Value,

                ProviderPaymentIntentId =
                    payment.ProviderPaymentIntentId,

                TuitionSubtotal =
                    bill.TuitionSubtotal,

                TuitionDiscountAmount =
                    bill.TuitionDiscountAmount,

                HardcopyFee =
                    bill.HardcopyFee,

                DeliveryFee =
                    bill.DeliveryFee,

                TotalAmount =
                    bill.TotalAmount,

                Items =
                    bill.Items
                        .OrderBy(item =>
                            item.BillItemId)
                        .Select(item =>
                            new PaymentReceiptItemViewModel
                            {
                                Description =
                                    item.Description,

                                ClassNames = item.ItemType == BillItemType.Tuition
                                    ? receiptClassNames.Where(name => !string.IsNullOrWhiteSpace(name))
                                        .Select(name => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(name!)).ToList()
                                    : new List<string>(),

                                Quantity =
                                    item.Quantity,

                                UnitAmount =
                                    item.UnitAmount,

                                LineTotal =
                                    item.LineTotal
                            })
                        .ToList()
            };
        }

        [AllowAnonymous]
        [EnableRateLimiting("PaymentPolicy")]
        [HttpGet]
        public IActionResult Cancelled(
            string? token,
            bool preserveSession = false)
        {
            ViewBag.PaymentAccessToken =
                token;

            ViewBag.PreserveSession =
                preserveSession;

            return View();
        }

        private async Task<string?> ValidateAcademicBill(SecureSmartTuitionSystem.Models.Billing.Bill bill)
        {
            var app = bill.EnrollmentApplication;
            var classes = await _context.ClassSessions.AsNoTracking().Where(c => _context.Set<SecureSmartTuitionSystem.Models.Enrollment.EnrollmentApplicationSubject>().Any(x => x.EnrollmentApplicationId == app.EnrollmentApplicationId && x.ClassSessionId == c.ClassSessionId)).ToListAsync();
            if (classes.Count == 0) return "No valid classes are linked to this bill.";
            if (app.RequestedStartDate.Date < ClassScheduleService.Today)
                return "The selected enrollment start date has passed. Admin must cancel the unpaid application so a new request can use a future start date.";
            return await new EnrollmentRulesService(_context).ValidateAsync(
                classes.Select(x => x.ClassSessionId).ToList(), app.RequestedStartDate,
                app.ExistingStudentProfileId, app.EnrollmentApplicationId,
                durationMonths: app.EnrollmentDurationMonths);
        }

        [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryEnrollment(int applicationId)
        {
            var payment = await _context.Payments.Include(x => x.Bill).ThenInclude(x => x.EnrollmentApplication)
                .FirstOrDefaultAsync(x => x.Bill.EnrollmentApplicationId == applicationId && x.Status == PaymentStatus.Succeeded && x.Bill.EnrollmentApplication.Status == SecureSmartTuitionSystem.Models.Enrollment.EnrollmentStatus.PaymentConfirmed);
            if (payment == null) return NotFound();
            try
            {
                if (payment.PaymentProvider == "Free") await _paymentCompletionService.CompleteZeroBillAsync(payment.BillId, HttpContext.RequestAborted);
                else
                {
                    var session = await new SessionService(_stripeClient).GetAsync(payment.ProviderCheckoutSessionId!);
                    if (session.PaymentStatus != "paid") return BadRequest();
                    await _paymentCompletionService.CompleteStripePaymentAsync(session.Id, session.PaymentIntentId, payment.ProviderEventId ?? $"recovery-{session.Id}", session.AmountTotal ?? 0, session.Currency ?? "myr", HttpContext.RequestAborted);
                }
                TempData["SuccessMessage"] = "Enrollment fulfillment completed without a new charge.";
            }
            catch (InvalidOperationException) { TempData["ErrorMessage"] = "Payment remains recorded as paid. Resolve enrollment/account availability before retrying fulfillment."; }
            return RedirectToAction("Details", "AdminEnrollments", new { id = applicationId });
        }

        private async Task MarkPaymentFailedAsync(
            Payment? payment,
            string failureReason)
        {
            if (payment == null ||
                payment.PaymentId <= 0)
            {
                return;
            }

            /*
             * The original transaction was rolled back,
             * so verify whether the payment still exists.
             */
            var savedPayment =
                await _context.Payments
                    .FirstOrDefaultAsync(item =>
                        item.PaymentId ==
                        payment.PaymentId);

            if (savedPayment == null)
            {
                return;
            }

            savedPayment.Status =
                PaymentStatus.Failed;

            savedPayment.FailedAt =
                DateTime.UtcNow;

            savedPayment.FailureReason =
                failureReason;

            await _context.SaveChangesAsync();
        }

        private async Task<string>
            GenerateNextPaymentNumberAsync()
        {
            var existingNumbers =
                await _context.Payments
                    .AsNoTracking()
                    .Select(payment =>
                        payment.PaymentNumber)
                    .ToListAsync();

            int highestNumber = 0;

            foreach (string paymentNumber in existingNumbers)
            {
                if (string.IsNullOrWhiteSpace(
                        paymentNumber) ||
                    paymentNumber.Length != 7 ||
                    !paymentNumber.StartsWith(
                        "PY",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(
                        paymentNumber.AsSpan(2),
                        out int number) &&
                    number > highestNumber)
                {
                    highestNumber = number;
                }
            }

            int nextNumber =
                highestNumber + 1;

            if (nextNumber > 99999)
            {
                throw new InvalidOperationException(
                    "The maximum payment number has been reached.");
            }

            return $"PY{nextNumber:D5}";
        }

        private async Task<string>
            GenerateUniqueAccessTokenAsync()
        {
            while (true)
            {
                string token =
                    PaymentAccessTokenService.GenerateToken();

                string tokenHash =
                    PaymentAccessTokenService.HashToken(token);

                bool exists =
                    await _context.Bills
                        .AnyAsync(bill =>
                            bill.PaymentAccessTokenHash ==
                            tokenHash);

                if (!exists)
                {
                    return token;
                }
            }
        }

    }
}
