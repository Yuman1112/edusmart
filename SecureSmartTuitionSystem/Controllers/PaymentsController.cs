using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.ViewModels.Billing;
using SecureSmartTuitionSystem.Services.Billing;
using Stripe;
using Stripe.Checkout;

namespace SecureSmartTuitionSystem.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StripeClient _stripeClient;

        private readonly IPaymentCompletionService
            _paymentCompletionService;

        private readonly ILogger<PaymentsController>
            _logger;

        public PaymentsController(
            ApplicationDbContext context,
            StripeClient stripeClient,
            IPaymentCompletionService paymentCompletionService,
            ILogger<PaymentsController> logger)
        {
            _context = context;
            _stripeClient = stripeClient;
            _paymentCompletionService =
                paymentCompletionService;
            _logger = logger;
        }

        /*
         * Admin can use this action to generate or open
         * the secure payment link for an existing bill.
         */
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> OpenBill(
            int id)
        {
            var bill =
                await _context.Bills
                    .FirstOrDefaultAsync(item =>
                        item.BillId == id);

            if (bill == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(
                    bill.PaymentAccessToken))
            {
                bill.PaymentAccessToken =
                    await GenerateUniqueAccessTokenAsync();

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(Checkout),
                new
                {
                    token = bill.PaymentAccessToken
                });
        }

        /*
         * This page supports applicants who do not yet
         * have a system account.
         *
         * Access is protected using a secure random token.
         */
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Checkout(
            string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return NotFound();
            }

            var bill =
                await _context.Bills
                    .AsNoTracking()
                    .Include(item =>
                        item.EnrollmentApplication)
                    .Include(item =>
                        item.Items)
                    .FirstOrDefaultAsync(item =>
                        item.PaymentAccessToken == token);

            if (bill == null)
            {
                return NotFound();
            }

            if (bill.Status == BillStatus.Paid)
            {
                TempData["SuccessMessage"] =
                    $"Bill {bill.BillNumber} has already been paid.";
            }

            if (bill.Status ==
                    BillStatus.PendingPayment &&
                bill.DueAt < DateTime.UtcNow)
            {
                var trackedBill =
                    await _context.Bills
                        .FirstAsync(item =>
                            item.BillId == bill.BillId);

                trackedBill.Status =
                    BillStatus.Overdue;

                await _context.SaveChangesAsync();

                bill.Status =
                    BillStatus.Overdue;
            }

            var application =
                bill.EnrollmentApplication;

            var viewModel =
                new PaymentCheckoutViewModel
                {
                    PaymentAccessToken =
                        bill.PaymentAccessToken!,

                    BillNumber =
                        bill.BillNumber,

                    ApplicationNumber =
                        application.ApplicationNumber,

                    StudentName =
                        application.StudentName,

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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            CreateCheckoutSession(
                string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return NotFound();
            }

            var bill =
                await _context.Bills
                    .Include(item =>
                        item.EnrollmentApplication)
                    .FirstOrDefaultAsync(item =>
                        item.PaymentAccessToken == token);

            if (bill == null)
            {
                return NotFound();
            }

            if (bill.Status == BillStatus.Paid)
            {
                TempData["SuccessMessage"] =
                    $"Bill {bill.BillNumber} has already been paid.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token
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
                        token
                    });
            }

            if (bill.TotalAmount <= 0)
            {
                TempData["ErrorMessage"] =
                    "The bill amount must be greater than RM0.00.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        token
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
                        token
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            Payment? payment = null;

            try
            {
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
                            "?session_id={CHECKOUT_SESSION_ID}",

                        CancelUrl =
                            $"{baseUrl}/Payments/Cancelled" +
                            $"?token={Uri.EscapeDataString(token)}",

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
                            $"edusmart-checkout-{payment.PaymentId}"
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
                        token
                    });
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

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
                        token
                    });
            }
        }

        /*
         * This page does not mark payment as successful.
         * Only the Stripe webhook can confirm payment.
         */
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Success(
            [FromQuery(Name = "session_id")]
    string? sessionId,
            CancellationToken cancellationToken)
        {
            ViewBag.PaymentVerified = false;
            ViewBag.BillId = null;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                ViewBag.StatusMessage =
                    "The Stripe Checkout Session ID is missing.";

                return View();
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

                    return View();
                }

                if (!session.AmountTotal.HasValue ||
                    session.AmountTotal.Value <= 0 ||
                    string.IsNullOrWhiteSpace(session.Currency))
                {
                    ViewBag.StatusMessage =
                        "Stripe returned incomplete payment information.";

                    return View();
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
                    $"Payment {result.PaymentNumber} was confirmed. " +
                    $"Bill {result.BillNumber} is now paid.";

                ViewBag.BillId =
                    result.BillId;

                _logger.LogInformation(
                    "Stripe Checkout Session {SessionId} " +
                    "was reconciled successfully for bill {BillNumber}.",
                    session.Id,
                    result.BillNumber);
            }
            catch (StripeException exception)
            {
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
                _logger.LogError(
                    exception,
                    "Local payment completion failed for " +
                    "Checkout Session {SessionId}.",
                    sessionId);

                ViewBag.StatusMessage =
                    exception.Message;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected payment completion error for " +
                    "Checkout Session {SessionId}.",
                    sessionId);

                ViewBag.StatusMessage =
                    "The payment was received, but EduSmart could " +
                    "not update the local payment record.";
            }

            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Cancelled(
            string? token)
        {
            ViewBag.PaymentAccessToken =
                token;

            return View();
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
                    GenerateAccessToken();

                bool exists =
                    await _context.Bills
                        .AnyAsync(bill =>
                            bill.PaymentAccessToken ==
                            token);

                if (!exists)
                {
                    return token;
                }
            }
        }

        private static string GenerateAccessToken()
        {
            byte[] tokenBytes =
                RandomNumberGenerator.GetBytes(32);

            return Convert
                .ToHexString(tokenBytes)
                .ToLowerInvariant();
        }
    }
}