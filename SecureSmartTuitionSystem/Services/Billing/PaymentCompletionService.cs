using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;

namespace SecureSmartTuitionSystem.Services.Billing
{
    public class PaymentCompletionService
        : IPaymentCompletionService
    {
        private readonly ApplicationDbContext _context;

        private readonly AccountProvisioningService
            _accountProvisioningService;

        private readonly UserManager<ApplicationUser>
            _userManager;

        private readonly IParentAccountSetupEmailService
            _parentAccountSetupEmailService;

        private readonly IStudentAccountSetupEmailService
            _studentAccountSetupEmailService;

        private readonly ILogger<PaymentCompletionService>
            _logger;

        public PaymentCompletionService(
            ApplicationDbContext context,
            AccountProvisioningService
                accountProvisioningService,
            UserManager<ApplicationUser>
                userManager,
            IParentAccountSetupEmailService
                parentAccountSetupEmailService,
            IStudentAccountSetupEmailService
                studentAccountSetupEmailService,
            ILogger<PaymentCompletionService> logger)
        {
            _context = context;

            _accountProvisioningService =
                accountProvisioningService;

            _userManager =
                userManager;

            _parentAccountSetupEmailService =
                parentAccountSetupEmailService;

            _studentAccountSetupEmailService =
                studentAccountSetupEmailService;

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
                 * A successful payment permanently invalidates the
                 * anonymous first-payment link.
                 */
                bill.PaymentAccessTokenHash = null;
                bill.PaymentAccessTokenExpiresAt = null;

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
                            "Payment was confirmed through Stripe."
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

                /*
                 * =====================================================
                 * PARENT ACCOUNT SETUP EMAIL
                 * =====================================================
                 */
                try
                {
                    ApplicationUser? parentUser =
                        await _userManager.FindByNameAsync(
                            provisioningResult.ParentUserName);

                    if (parentUser != null &&
                        parentUser.IsActive &&
                        parentUser.MustChangePassword &&
                        !string.IsNullOrWhiteSpace(
                            parentUser.Email))
                    {
                        ApplicationUser? studentUser =
                            await _userManager.FindByNameAsync(
                                provisioningResult.StudentUserName);

                        string token =
                            await _userManager
                                .GeneratePasswordResetTokenAsync(
                                    parentUser);

                        string encodedToken =
                            WebEncoders.Base64UrlEncode(
                                Encoding.UTF8.GetBytes(token));

                        string setupUrl =
                            "https://localhost:7086" +
                            "/Identity/Account/SetupPassword" +
                            $"?userId={Uri.EscapeDataString(parentUser.Id)}" +
                            $"&token={Uri.EscapeDataString(encodedToken)}";

                        string guardianName =
                            parentUser.FullName
                            ?? provisioningResult.ParentUserName;

                        string studentName =
                            studentUser?.FullName
                            ?? provisioningResult.StudentUserName;

                        await _parentAccountSetupEmailService.SendAsync(
                            parentUser.Email,
                            guardianName,
                            provisioningResult.ParentUserName,
                            studentName,
                            setupUrl,
                            cancellationToken);

                        _logger.LogInformation(
                            "Account setup email was sent to parent " +
                            "{ParentUserName} for application " +
                            "{ApplicationNumber}.",
                            provisioningResult.ParentUserName,
                            application.ApplicationNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Account setup email could not be sent " +
                        "for application {ApplicationNumber}.",
                        application.ApplicationNumber);
                }

                /*
                 * =====================================================
                 * STUDENT ACCOUNT SETUP EMAIL
                 * =====================================================
                 */
                try
                {
                    ApplicationUser? studentUser =
                        await _userManager.FindByNameAsync(
                            provisioningResult.StudentUserName);

                    if (studentUser != null &&
                        studentUser.IsActive &&
                        studentUser.MustChangePassword)
                    {
                        string? recipientEmail;
                        bool sentToGuardian;

                        if (!string.IsNullOrWhiteSpace(
                                studentUser.Email))
                        {
                            recipientEmail =
                                studentUser.Email;

                            sentToGuardian =
                                false;
                        }
                        else
                        {
                            recipientEmail =
                                application.GuardianEmail;

                            sentToGuardian =
                                true;
                        }

                        if (!string.IsNullOrWhiteSpace(
                                recipientEmail))
                        {
                            string token =
                                await _userManager
                                    .GeneratePasswordResetTokenAsync(
                                        studentUser);

                            string encodedToken =
                                WebEncoders.Base64UrlEncode(
                                    Encoding.UTF8.GetBytes(token));

                            string setupUrl =
                                "https://localhost:7086" +
                                "/Identity/Account/SetupPassword" +
                                $"?userId={Uri.EscapeDataString(studentUser.Id)}" +
                                $"&token={Uri.EscapeDataString(encodedToken)}";

                            string studentName =
                                studentUser.FullName
                                ?? provisioningResult.StudentUserName;

                            await _studentAccountSetupEmailService
                                .SendAsync(
                                    recipientEmail,
                                    studentName,
                                    provisioningResult.StudentUserName,
                                    setupUrl,
                                    sentToGuardian,
                                    cancellationToken);

                            _logger.LogInformation(
                                "Student account setup email was sent " +
                                "for student {StudentUserName} " +
                                "to {RecipientType} for application " +
                                "{ApplicationNumber}.",
                                provisioningResult.StudentUserName,
                                sentToGuardian
                                    ? "guardian"
                                    : "student",
                                application.ApplicationNumber);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Student account setup email could not be sent " +
                        "for application {ApplicationNumber}.",
                        application.ApplicationNumber);
                }

                /*
                 * =====================================================
                 * PAYMENT COMPLETION LOG
                 * =====================================================
                 */
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
