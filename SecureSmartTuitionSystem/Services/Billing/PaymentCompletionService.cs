using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Authentication;

namespace SecureSmartTuitionSystem.Services.Billing
{
    public class PaymentCompletionService
        : IPaymentCompletionService
    {
        private readonly ApplicationDbContext _context;

        private readonly AccountProvisioningService
            _accountProvisioningService;

        private readonly ILogger<PaymentCompletionService>
            _logger;

        public PaymentCompletionService(
            ApplicationDbContext context,
            AccountProvisioningService
                accountProvisioningService,
            ILogger<PaymentCompletionService> logger)
        {
            _context = context;
            _accountProvisioningService =
                accountProvisioningService;
            _logger = logger;
        }

        public async Task<PaymentCompletionResult>
            CompleteStripePaymentAsync(
                string checkoutSessionId,
                string? paymentIntentId,
                string stripeEventId,
                long amountPaidInSen,
                string currency,
                CancellationToken cancellationToken = default)
        {
            ValidateInput(
                checkoutSessionId,
                stripeEventId,
                amountPaidInSen,
                currency);

            IDbContextTransaction? transaction = null;

            bool ownsTransaction =
                _context.Database.CurrentTransaction == null;

            try
            {
                if (ownsTransaction)
                {
                    transaction =
                        await _context.Database
                            .BeginTransactionAsync(
                                IsolationLevel.Serializable,
                                cancellationToken);
                }

                /*
                 * Check whether Stripe already sent and processed
                 * this event.
                 */
                var paymentWithSameEvent =
                    await _context.Payments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            payment =>
                                payment.ProviderEventId ==
                                stripeEventId,
                            cancellationToken);

                if (paymentWithSameEvent != null)
                {
                    var existingResult =
                        await BuildExistingResultAsync(
                            paymentWithSameEvent.PaymentId,
                            cancellationToken);

                    existingResult.AlreadyProcessed = true;

                    if (ownsTransaction &&
                        transaction != null)
                    {
                        await transaction.CommitAsync(
                            cancellationToken);
                    }

                    return existingResult;
                }

                var payment =
                    await _context.Payments
                        .Include(item =>
                            item.Bill)
                        .ThenInclude(bill =>
                            bill.EnrollmentApplication)
                        .FirstOrDefaultAsync(
                            item =>
                                item.ProviderCheckoutSessionId ==
                                checkoutSessionId,
                            cancellationToken);

                if (payment == null)
                {
                    throw new InvalidOperationException(
                        "No payment record matches the " +
                        "Stripe Checkout Session.");
                }

                Bill bill = payment.Bill;

                EnrollmentApplication application =
                    bill.EnrollmentApplication;

                /*
                 * Handle Stripe retrying the webhook after
                 * payment was already fully processed.
                 */
                if (payment.Status ==
                        PaymentStatus.Succeeded &&
                    bill.Status ==
                        BillStatus.Paid &&
                    application.Status ==
                        EnrollmentStatus.Enrolled)
                {
                    var completedResult =
                        await BuildExistingResultAsync(
                            payment.PaymentId,
                            cancellationToken);

                    completedResult.AlreadyProcessed = true;

                    if (ownsTransaction &&
                        transaction != null)
                    {
                        await transaction.CommitAsync(
                            cancellationToken);
                    }

                    return completedResult;
                }

                ValidatePaymentState(
                    payment,
                    bill,
                    application);

                ValidatePaymentAmount(
                    bill,
                    amountPaidInSen,
                    currency);

                DateTime completedAt =
                    DateTime.UtcNow;

                /*
                 * Mark the Payment as successful.
                 */
                payment.Status =
                    PaymentStatus.Succeeded;

                payment.ProviderPaymentIntentId =
                    string.IsNullOrWhiteSpace(
                        paymentIntentId)
                        ? null
                        : paymentIntentId;

                payment.ProviderEventId =
                    stripeEventId;

                payment.CompletedAt =
                    completedAt;

                payment.FailedAt =
                    null;

                payment.FailureReason =
                    null;

                /*
                 * Mark the Bill as paid.
                 */
                bill.Status =
                    BillStatus.Paid;

                bill.PaidAt =
                    completedAt;

                /*
                 * Record payment confirmation.
                 */
                EnrollmentStatus previousStatus =
                    application.Status;

                application.Status =
                    EnrollmentStatus.PaymentConfirmed;

                _context.EnrollmentStatusHistories.Add(
                    new EnrollmentStatusHistory
                    {
                        EnrollmentApplicationId =
                            application
                                .EnrollmentApplicationId,

                        PreviousStatus =
                            previousStatus,

                        NewStatus =
                            EnrollmentStatus
                                .PaymentConfirmed,

                        ChangedAt =
                            completedAt,

                        ChangedByUserId =
                            null,

                        Remarks =
                            $"Payment {payment.PaymentNumber} " +
                            "was confirmed through Stripe."
                    });

                await _context.SaveChangesAsync(
                    cancellationToken);

                /*
                 * Create or reuse Parent and Student accounts,
                 * profiles and class enrollments.
                 */
                AccountProvisioningResult
                    provisioningResult =
                        await _accountProvisioningService
                            .ProvisionEnrollmentAsync(
                                application
                                    .EnrollmentApplicationId,
                                cancellationToken);

                /*
                 * Complete the enrollment application.
                 */
                application.Status =
                    EnrollmentStatus.Enrolled;

                _context.EnrollmentStatusHistories.Add(
                    new EnrollmentStatusHistory
                    {
                        EnrollmentApplicationId =
                            application
                                .EnrollmentApplicationId,

                        PreviousStatus =
                            EnrollmentStatus
                                .PaymentConfirmed,

                        NewStatus =
                            EnrollmentStatus.Enrolled,

                        ChangedAt =
                            DateTime.UtcNow,

                        ChangedByUserId =
                            null,

                        Remarks =
                            "Accounts and class enrollments " +
                            "were provisioned after payment."
                    });

                await _context.SaveChangesAsync(
                    cancellationToken);

                if (ownsTransaction &&
                    transaction != null)
                {
                    await transaction.CommitAsync(
                        cancellationToken);
                }

                _logger.LogInformation(
                    "Payment {PaymentNumber} completed for " +
                    "bill {BillNumber} and application " +
                    "{ApplicationNumber}.",
                    payment.PaymentNumber,
                    bill.BillNumber,
                    application.ApplicationNumber);

                return new PaymentCompletionResult
                {
                    AlreadyProcessed =
                        false,

                    PaymentId =
                        payment.PaymentId,

                    PaymentNumber =
                        payment.PaymentNumber,

                    BillId =
                        bill.BillId,

                    BillNumber =
                        bill.BillNumber,

                    EnrollmentApplicationId =
                        application
                            .EnrollmentApplicationId,

                    ApplicationNumber =
                        application.ApplicationNumber,

                    ParentUserName =
                        provisioningResult
                            .ParentUserName,

                    StudentUserName =
                        provisioningResult
                            .StudentUserName,

                    CreatedEnrollmentCount =
                        provisioningResult
                            .CreatedEnrollmentCount
                };
            }
            catch
            {
                if (ownsTransaction &&
                    transaction != null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        private static void ValidateInput(
            string checkoutSessionId,
            string stripeEventId,
            long amountPaidInSen,
            string currency)
        {
            if (string.IsNullOrWhiteSpace(
                    checkoutSessionId))
            {
                throw new ArgumentException(
                    "Stripe Checkout Session ID is required.",
                    nameof(checkoutSessionId));
            }

            if (string.IsNullOrWhiteSpace(
                    stripeEventId))
            {
                throw new ArgumentException(
                    "Stripe Event ID is required.",
                    nameof(stripeEventId));
            }

            if (amountPaidInSen <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amountPaidInSen),
                    "The paid amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentException(
                    "Payment currency is required.",
                    nameof(currency));
            }
        }

        private static void ValidatePaymentState(
            Payment payment,
            Bill bill,
            EnrollmentApplication application)
        {
            if (payment.Status !=
                PaymentStatus.Pending)
            {
                throw new InvalidOperationException(
                    $"Payment {payment.PaymentNumber} " +
                    $"has status {payment.Status} and " +
                    "cannot be completed.");
            }

            if (bill.Status !=
                    BillStatus.PendingPayment &&
                bill.Status !=
                    BillStatus.Overdue)
            {
                throw new InvalidOperationException(
                    $"Bill {bill.BillNumber} has status " +
                    $"{bill.Status} and cannot be paid.");
            }

            if (application.Status !=
                EnrollmentStatus.ApprovedPendingPayment)
            {
                throw new InvalidOperationException(
                    $"Application " +
                    $"{application.ApplicationNumber} " +
                    $"has status {application.Status} and " +
                    "cannot complete payment.");
            }
        }

        private static void ValidatePaymentAmount(
            Bill bill,
            long amountPaidInSen,
            string currency)
        {
            long expectedAmountInSen =
                checked(
                    (long)decimal.Round(
                        bill.TotalAmount * 100m,
                        0,
                        MidpointRounding.AwayFromZero));

            if (amountPaidInSen !=
                expectedAmountInSen)
            {
                throw new InvalidOperationException(
                    "The Stripe payment amount does not " +
                    "match the bill total.");
            }

            if (!string.Equals(
                    currency,
                    bill.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The Stripe payment currency does not " +
                    "match the bill currency.");
            }
        }

        private async Task<PaymentCompletionResult>
            BuildExistingResultAsync(
                int paymentId,
                CancellationToken cancellationToken)
        {
            var payment =
                await _context.Payments
                    .AsNoTracking()
                    .Include(item =>
                        item.Bill)
                    .ThenInclude(bill =>
                        bill.EnrollmentApplication)
                    .FirstOrDefaultAsync(
                        item =>
                            item.PaymentId == paymentId,
                        cancellationToken);

            if (payment == null)
            {
                throw new InvalidOperationException(
                    "The processed payment record " +
                    "was not found.");
            }

            var application =
                payment.Bill.EnrollmentApplication;

            string parentUserName =
                string.Empty;

            string studentUserName =
                string.Empty;

            if (application.ExistingParentProfileId
                .HasValue)
            {
                parentUserName =
                    await _context.ParentProfiles
                        .AsNoTracking()
                        .Where(profile =>
                            profile.ParentProfileId ==
                            application
                                .ExistingParentProfileId
                                .Value)
                        .Select(profile =>
                            profile.ApplicationUser
                                .UserName ?? string.Empty)
                        .FirstOrDefaultAsync(
                            cancellationToken)
                    ?? string.Empty;
            }

            if (application.ExistingStudentProfileId
                .HasValue)
            {
                studentUserName =
                    await _context.StudentProfiles
                        .AsNoTracking()
                        .Where(profile =>
                            profile.StudentProfileId ==
                            application
                                .ExistingStudentProfileId
                                .Value)
                        .Select(profile =>
                            profile.ApplicationUser != null
                                ? profile.ApplicationUser
                                    .UserName
                                : string.Empty)
                        .FirstOrDefaultAsync(
                            cancellationToken)
                    ?? string.Empty;
            }

            return new PaymentCompletionResult
            {
                AlreadyProcessed =
                    true,

                PaymentId =
                    payment.PaymentId,

                PaymentNumber =
                    payment.PaymentNumber,

                BillId =
                    payment.Bill.BillId,

                BillNumber =
                    payment.Bill.BillNumber,

                EnrollmentApplicationId =
                    application.EnrollmentApplicationId,

                ApplicationNumber =
                    application.ApplicationNumber,

                ParentUserName =
                    parentUserName,

                StudentUserName =
                    studentUserName,

                CreatedEnrollmentCount =
                    0
            };
        }
    }
}