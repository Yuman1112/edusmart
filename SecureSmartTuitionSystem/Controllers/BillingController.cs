using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.ViewModels.Billing;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            BillStatus? status,
            string sortBy = "issuedAt",
            string sortDirection = "desc")
        {
            sortBy = NormalizeSortBy(sortBy);

            sortDirection =
                NormalizeSortDirection(sortDirection);

            DateTime currentTime = DateTime.UtcNow;

            /*
             * Mark expired pending bills as overdue.
             */
            var expiredBills =
                await _context.Bills
                    .Where(bill =>
                        bill.Status ==
                            BillStatus.PendingPayment &&
                        bill.DueAt < currentTime)
                    .ToListAsync();

            if (expiredBills.Count > 0)
            {
                foreach (var bill in expiredBills)
                {
                    bill.Status =
                        BillStatus.Overdue;
                }

                await _context.SaveChangesAsync();
            }

            var baseQuery =
                _context.Bills
                    .AsNoTracking();

            int totalBillCount =
                await baseQuery.CountAsync();

            int pendingBillCount =
                await baseQuery.CountAsync(bill =>
                    bill.Status ==
                    BillStatus.PendingPayment);

            int paidBillCount =
                await baseQuery.CountAsync(bill =>
                    bill.Status ==
                    BillStatus.Paid);

            int overdueBillCount =
                await baseQuery.CountAsync(bill =>
                    bill.Status ==
                    BillStatus.Overdue);

            decimal totalPendingAmount =
                await baseQuery
                    .Where(bill =>
                        bill.Status ==
                            BillStatus.PendingPayment ||
                        bill.Status ==
                            BillStatus.Overdue)
                    .SumAsync(bill =>
                        (decimal?)bill.TotalAmount)
                    ?? 0m;

            var query =
                baseQuery.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(bill =>
                    bill.Status == status.Value);
            }

            query = ApplySorting(
                query,
                sortBy,
                sortDirection);

            var bills =
                await query
                    .Select(bill =>
                        new BillingListItemViewModel
                        {
                            BillId =
                                bill.BillId,

                            BillNumber =
                                bill.BillNumber,

                            ApplicationNumber =
                                bill.EnrollmentApplication
                                    .ApplicationNumber,

                            StudentName =
                                bill.EnrollmentApplication
                                    .StudentName,

                            GuardianName =
                                bill.EnrollmentApplication
                                    .GuardianName,

                            GuardianEmail =
                                bill.EnrollmentApplication
                                    .GuardianEmail,

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

                            PaidAt =
                                bill.PaidAt
                        })
                    .ToListAsync();

            var viewModel =
                new BillingIndexViewModel
                {
                    TotalBillCount =
                        totalBillCount,

                    PendingBillCount =
                        pendingBillCount,

                    PaidBillCount =
                        paidBillCount,

                    OverdueBillCount =
                        overdueBillCount,

                    TotalPendingAmount =
                        totalPendingAmount,

                    SelectedStatus =
                        status,

                    SortBy =
                        sortBy,

                    SortDirection =
                        sortDirection,

                    Bills =
                        bills
                };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Details(
            int id)
        {
            var bill =
                await _context.Bills
                    .AsNoTracking()
                    .Include(item =>
                        item.EnrollmentApplication)
                    .Include(item =>
                        item.Items)
                    .Include(item =>
                        item.Payments)
                    .FirstOrDefaultAsync(item =>
                        item.BillId == id);

            if (bill == null)
            {
                return NotFound();
            }

            /*
             * Update a pending bill to overdue when
             * its due date has passed.
             */
            if (bill.Status ==
                    BillStatus.PendingPayment &&
                bill.DueAt < DateTime.UtcNow)
            {
                var trackedBill =
                    await _context.Bills
                        .FirstAsync(item =>
                            item.BillId == id);

                trackedBill.Status =
                    BillStatus.Overdue;

                await _context.SaveChangesAsync();

                bill.Status =
                    BillStatus.Overdue;
            }

            var application =
                bill.EnrollmentApplication;

            var viewModel =
                new BillDetailsViewModel
                {
                    BillId =
                        bill.BillId,

                    BillNumber =
                        bill.BillNumber,

                    ApplicationNumber =
                        application.ApplicationNumber,

                    EnrollmentApplicationId =
                        application.EnrollmentApplicationId,

                    StudentName =
                        application.StudentName,

                    GuardianName =
                        application.GuardianName,

                    GuardianEmail =
                        application.GuardianEmail,

                    GuardianPhoneNumber =
                        application.GuardianPhoneNumber,

                    AcademicLevel =
                        application.AcademicLevel,

                    EnrollmentDurationMonths =
                        application.EnrollmentDurationMonths,

                    EnrollmentStatus =
                        application.Status,

                    MonthlyTuitionFee =
                        application
                            .MonthlyTuitionFeeAtApplication,

                    TuitionSubtotal =
                        bill.TuitionSubtotal,

                    TuitionDiscountRate =
                        bill.TuitionDiscountRate,

                    TuitionDiscountAmount =
                        bill.TuitionDiscountAmount,

                    DiscountedTuitionAmount =
                        bill.DiscountedTuitionAmount,

                    HardcopySubjectCount =
                        application
                            .HardcopySubjectCountAtApplication,

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

                    PaidAt =
                        bill.PaidAt,

                    CancelledAt =
                        bill.CancelledAt,

                    CancellationReason =
                        bill.CancellationReason,

                    HardcopyMaterialsRequested =
                        application
                            .HardcopyMaterialsRequested,

                    DeliveryRecipientName =
                        application
                            .DeliveryRecipientName,

                    DeliveryAddressLine1 =
                        application
                            .DeliveryAddressLine1,

                    DeliveryAddressLine2 =
                        application
                            .DeliveryAddressLine2,

                    DeliveryPostcode =
                        application
                            .DeliveryPostcode,

                    DeliveryCity =
                        application
                            .DeliveryCity,

                    DeliveryState =
                        application
                            .DeliveryState,

                    DeliveryRegion =
                        application
                            .DeliveryRegion,

                    Items =
                        bill.Items
                            .OrderBy(item =>
                                item.BillItemId)
                            .Select(item =>
                                new BillItemViewModel
                                {
                                    ItemType =
                                        item.ItemType,

                                    Description =
                                        item.Description,

                                    Quantity =
                                        item.Quantity,

                                    UnitAmount =
                                        item.UnitAmount,

                                    LineTotal =
                                        item.LineTotal
                                })
                            .ToList(),

                    Payments =
                        bill.Payments
                            .OrderByDescending(payment =>
                                payment.CreatedAt)
                            .Select(payment =>
                                new BillPaymentViewModel
                                {
                                    PaymentNumber =
                                        payment.PaymentNumber,

                                    Amount =
                                        payment.Amount,

                                    Currency =
                                        payment.Currency,

                                    PaymentProvider =
                                        payment.PaymentProvider,

                                    Status =
                                        payment.Status,

                                    CreatedAt =
                                        payment.CreatedAt,

                                    CompletedAt =
                                        payment.CompletedAt,

                                    FailureReason =
                                        payment.FailureReason
                                })
                            .ToList()
                };

            return View(viewModel);
        }

        private static IQueryable<Bill> ApplySorting(
            IQueryable<Bill> query,
            string sortBy,
            string sortDirection)
        {
            bool descending =
                sortDirection == "desc";

            return sortBy switch
            {
                "billNumber" =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.BillNumber)
                        : query.OrderBy(bill =>
                            bill.BillNumber),

                "applicationNumber" =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.EnrollmentApplication
                                .ApplicationNumber)
                        : query.OrderBy(bill =>
                            bill.EnrollmentApplication
                                .ApplicationNumber),

                "studentName" =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.EnrollmentApplication
                                .StudentName)
                        : query.OrderBy(bill =>
                            bill.EnrollmentApplication
                                .StudentName),

                "amount" =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.TotalAmount)
                        : query.OrderBy(bill =>
                            bill.TotalAmount),

                "dueAt" =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.DueAt)
                        : query.OrderBy(bill =>
                            bill.DueAt),

                "status" =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.Status)
                        : query.OrderBy(bill =>
                            bill.Status),

                _ =>
                    descending
                        ? query.OrderByDescending(bill =>
                            bill.IssuedAt)
                        : query.OrderBy(bill =>
                            bill.IssuedAt)
            };
        }

        private static string NormalizeSortBy(
            string? sortBy)
        {
            string[] allowedValues =
            {
                "billNumber",
                "applicationNumber",
                "studentName",
                "amount",
                "issuedAt",
                "dueAt",
                "status"
            };

            return allowedValues.Contains(sortBy)
                ? sortBy!
                : "issuedAt";
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
    }
}