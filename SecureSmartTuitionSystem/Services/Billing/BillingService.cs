using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;
using System.Security.Cryptography;

namespace SecureSmartTuitionSystem.Services.Billing
{
    public class BillingService : IBillingService
    {
        private const decimal HardcopyUnitPrice = 10.00m;

        private readonly ApplicationDbContext _context;

        public BillingService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Bill?>
            GetBillByApplicationIdAsync(
                int enrollmentApplicationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Bills
                .AsNoTracking()
                .Include(bill => bill.Items)
                .Include(bill => bill.Payments)
                .FirstOrDefaultAsync(
                    bill =>
                        bill.EnrollmentApplicationId ==
                        enrollmentApplicationId,
                    cancellationToken);
        }

        public async Task<Bill> GenerateBillAsync(
            int enrollmentApplicationId,
            CancellationToken cancellationToken = default)
        {
            if (enrollmentApplicationId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enrollmentApplicationId),
                    "A valid enrollment application ID is required.");
            }

            /*
             * Return the existing bill when one has
             * already been generated.
             */
            var existingBill =
                await FindExistingBillAsync(
                    enrollmentApplicationId,
                    cancellationToken);

            if (existingBill != null)
            {
                return existingBill;
            }

            var application =
                await _context.EnrollmentApplications
                    .Include(enrollment =>
                        enrollment.SelectedSubjects)
                    .ThenInclude(selected =>
                        selected.ClassSession)
                    .ThenInclude(session =>
                        session.Subject)
                    .FirstOrDefaultAsync(
                        enrollment =>
                            enrollment.EnrollmentApplicationId ==
                            enrollmentApplicationId,
                        cancellationToken);

            if (application == null)
            {
                throw new InvalidOperationException(
                    "The enrollment application was not found.");
            }

            if (application.SelectedSubjects.Count == 0)
            {
                throw new InvalidOperationException(
                    "A bill cannot be generated because the " +
                    "application has no selected classes.");
            }

            if (application.EnrollmentDurationMonths <= 0)
            {
                throw new InvalidOperationException(
                    "The enrollment duration is invalid.");
            }

            ValidateFeeSnapshot(application);

            /*
             * AdminEnrollmentsController may already have
             * started a transaction.
             *
             * BillingService only creates and commits a new
             * transaction when there is no current transaction.
             */
            bool ownsTransaction =
                _context.Database.CurrentTransaction == null;

            IDbContextTransaction? transaction = null;

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
                 * Check again inside the transaction to prevent
                 * duplicate bills.
                 */
                existingBill =
                    await FindExistingBillAsync(
                        enrollmentApplicationId,
                        cancellationToken);

                if (existingBill != null)
                {
                    if (ownsTransaction &&
                        transaction != null)
                    {
                        await transaction.CommitAsync(
                            cancellationToken);
                    }

                    return existingBill;
                }

                string billNumber =
                    await GenerateNextBillNumberAsync(
                        cancellationToken);

                DateTime issuedAt = DateTime.UtcNow;

                var bill = new Bill
                {
                    BillNumber = billNumber,

                    PaymentAccessToken =
                        GeneratePaymentAccessToken(),

                    EnrollmentApplicationId =
                        application.EnrollmentApplicationId,

                    TuitionSubtotal =
                        application
                            .TuitionSubtotalAtApplication,

                    TuitionDiscountRate =
                        application
                            .DiscountRateAtApplication,

                    TuitionDiscountAmount =
                        application
                            .DiscountAmountAtApplication,

                    DiscountedTuitionAmount =
                        application
                            .DiscountedTuitionAtApplication,

                    HardcopyFee =
                        application
                            .HardcopyFeeAtApplication,

                    DeliveryFee =
                        application
                            .ShippingFeeAtApplication,

                    TotalAmount =
                        application
                            .EstimatedTotalAtApplication,

                    Currency = "MYR",

                    Status =
                        BillStatus.PendingPayment,

                    IssuedAt = issuedAt,

                    DueAt = issuedAt.AddDays(7)
                };

                AddTuitionItem(
                    bill,
                    application);

                AddDiscountItem(
                    bill,
                    application);

                AddHardcopyItem(
                    bill,
                    application);

                AddDeliveryItem(
                    bill,
                    application);

                ValidateBillTotal(bill);

                _context.Bills.Add(bill);

                await _context.SaveChangesAsync(
                    cancellationToken);

                if (ownsTransaction &&
                    transaction != null)
                {
                    await transaction.CommitAsync(
                        cancellationToken);
                }

                return bill;
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

        private async Task<Bill?>
            FindExistingBillAsync(
                int enrollmentApplicationId,
                CancellationToken cancellationToken)
        {
            return await _context.Bills
                .Include(bill => bill.Items)
                .Include(bill => bill.Payments)
                .FirstOrDefaultAsync(
                    bill =>
                        bill.EnrollmentApplicationId ==
                        enrollmentApplicationId,
                    cancellationToken);
        }

        private static void AddTuitionItem(
            Bill bill,
            EnrollmentApplication application)
        {
            int durationMonths =
                application.EnrollmentDurationMonths;

            DateTime startMonth =
                new DateTime(
                    application.RequestedStartDate.Year,
                    application.RequestedStartDate.Month,
                    1);

            DateTime endMonth =
                startMonth.AddMonths(
                    durationMonths - 1);

            string coveredPeriod;

            if (durationMonths == 1)
            {
                coveredPeriod =
                    startMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture);
            }
            else
            {
                string startMonthText =
                    startMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture);

                string endMonthText =
                    endMonth.ToString(
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture);

                coveredPeriod =
                    $"{startMonthText} – {endMonthText}";
            }

            bill.Items.Add(
                new BillItem
                {
                    ItemType =
                        BillItemType.Tuition,

                    Description =
                        $"{durationMonths}-month " +
                        $"online tuition fee ({coveredPeriod})",

                    Quantity =
                        durationMonths,

                    UnitAmount =
                        application
                            .MonthlyTuitionFeeAtApplication,

                    LineTotal =
                        application
                            .TuitionSubtotalAtApplication
                });
        }

        private static void AddDiscountItem(
            Bill bill,
            EnrollmentApplication application)
        {
            if (application.DiscountAmountAtApplication <= 0)
            {
                return;
            }

            bill.Items.Add(
                new BillItem
                {
                    ItemType =
                        BillItemType.TuitionDiscount,

                    Description =
                        "Tuition discount " +
                        $"({application.DiscountRateAtApplication:F0}%)",

                    Quantity = 1,

                    UnitAmount =
                        -application
                            .DiscountAmountAtApplication,

                    LineTotal =
                        -application
                            .DiscountAmountAtApplication
                });
        }

        private static void AddHardcopyItem(
            Bill bill,
            EnrollmentApplication application)
        {
            if (!application.HardcopyMaterialsRequested ||
                application.HardcopyFeeAtApplication <= 0)
            {
                return;
            }

            int quantity =
                application.HardcopySubjectCountAtApplication *
                application.EnrollmentDurationMonths;

            if (quantity <= 0)
            {
                throw new InvalidOperationException(
                    "The saved hardcopy quantity is invalid.");
            }

            decimal expectedHardcopyFee =
                quantity * HardcopyUnitPrice;

            if (!AmountsMatch(
                    expectedHardcopyFee,
                    application.HardcopyFeeAtApplication))
            {
                throw new InvalidOperationException(
                    "The saved hardcopy fee is inconsistent.");
            }

            bill.Items.Add(
                new BillItem
                {
                    ItemType =
                        BillItemType.HardcopyMaterials,

                    Description =
                        "Hardcopy learning materials " +
                        $"({application.HardcopySubjectCountAtApplication} " +
                        "subject(s) × " +
                        $"{application.EnrollmentDurationMonths} month(s))",

                    Quantity = quantity,

                    UnitAmount = HardcopyUnitPrice,

                    LineTotal =
                        application
                            .HardcopyFeeAtApplication
                });
        }

        private static void AddDeliveryItem(
            Bill bill,
            EnrollmentApplication application)
        {
            if (!application.HardcopyMaterialsRequested ||
                application.ShippingFeeAtApplication <= 0)
            {
                return;
            }

            string region =
                string.IsNullOrWhiteSpace(
                    application.DeliveryRegion)
                    ? "Malaysia"
                    : application.DeliveryRegion;

            bill.Items.Add(
                new BillItem
                {
                    ItemType =
                        BillItemType.Delivery,

                    Description =
                        $"Hardcopy delivery fee ({region})",

                    Quantity = 1,

                    UnitAmount =
                        application
                            .ShippingFeeAtApplication,

                    LineTotal =
                        application
                            .ShippingFeeAtApplication
                });
        }

        private static void ValidateFeeSnapshot(
            EnrollmentApplication application)
        {
            if (application.MonthlyTuitionFeeAtApplication < 0 ||
                application.TuitionSubtotalAtApplication < 0 ||
                application.DiscountRateAtApplication < 0 ||
                application.DiscountRateAtApplication > 100 ||
                application.DiscountAmountAtApplication < 0 ||
                application.DiscountedTuitionAtApplication < 0 ||
                application.HardcopyFeeAtApplication < 0 ||
                application.ShippingFeeAtApplication < 0 ||
                application.EstimatedTotalAtApplication < 0)
            {
                throw new InvalidOperationException(
                    "The application contains an invalid fee snapshot.");
            }

            decimal expectedTuitionSubtotal =
                application.MonthlyTuitionFeeAtApplication *
                application.EnrollmentDurationMonths;

            if (!AmountsMatch(
                    expectedTuitionSubtotal,
                    application.TuitionSubtotalAtApplication))
            {
                throw new InvalidOperationException(
                    "The saved tuition subtotal is inconsistent.");
            }

            decimal expectedDiscountedTuition =
                application.TuitionSubtotalAtApplication -
                application.DiscountAmountAtApplication;

            if (!AmountsMatch(
                    expectedDiscountedTuition,
                    application.DiscountedTuitionAtApplication))
            {
                throw new InvalidOperationException(
                    "The saved discounted tuition amount " +
                    "is inconsistent.");
            }

            decimal expectedTotal =
                application.DiscountedTuitionAtApplication +
                application.HardcopyFeeAtApplication +
                application.ShippingFeeAtApplication;

            if (!AmountsMatch(
                    expectedTotal,
                    application.EstimatedTotalAtApplication))
            {
                throw new InvalidOperationException(
                    "The saved estimated total is inconsistent.");
            }

            if (!application.HardcopyMaterialsRequested &&
                (application.HardcopyFeeAtApplication != 0 ||
                 application.ShippingFeeAtApplication != 0))
            {
                throw new InvalidOperationException(
                    "Hardcopy and delivery fees cannot be charged " +
                    "when hardcopy materials were not requested.");
            }
        }

        private static void ValidateBillTotal(
            Bill bill)
        {
            decimal itemTotal =
                bill.Items.Sum(item =>
                    item.LineTotal);

            if (!AmountsMatch(
                    itemTotal,
                    bill.TotalAmount))
            {
                throw new InvalidOperationException(
                    "The bill items do not match the final total.");
            }
        }

        private async Task<string>
            GenerateNextBillNumberAsync(
                CancellationToken cancellationToken)
        {
            var existingNumbers =
                await _context.Bills
                    .AsNoTracking()
                    .Select(bill =>
                        bill.BillNumber)
                    .ToListAsync(
                        cancellationToken);

            int highestNumber = 0;

            foreach (string billNumber in existingNumbers)
            {
                if (string.IsNullOrWhiteSpace(billNumber) ||
                    billNumber.Length != 7 ||
                    !billNumber.StartsWith(
                        "BL",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(
                        billNumber.AsSpan(2),
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
                    "The maximum bill number has been reached.");
            }

            return $"BL{nextNumber:D5}";
        }

        private static bool AmountsMatch(
            decimal first,
            decimal second)
        {
            return Math.Abs(first - second) < 0.01m;
        }

        private static string GeneratePaymentAccessToken()
        {
            byte[] tokenBytes =
                RandomNumberGenerator.GetBytes(32);

            return Convert
                .ToHexString(tokenBytes)
                .ToLowerInvariant();
        }
    }
}