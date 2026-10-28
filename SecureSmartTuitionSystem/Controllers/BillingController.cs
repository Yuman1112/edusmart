using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.ViewModels.Billing;
using SecureSmartTuitionSystem.Services.Billing;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin,Parent,Student")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MonthlyBillingService _monthlyBilling;

        public BillingController(
            ApplicationDbContext context, MonthlyBillingService monthlyBilling)
        {
            _context = context;
            _monthlyBilling = monthlyBilling;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            BillStatus? status,
            string? searchTerm,
            string sortBy = "issuedAt",
            string sortDirection = "desc")
        {
            searchTerm = (searchTerm ?? string.Empty).Trim();

            if (searchTerm.Length > 100)
            {
                searchTerm = searchTerm[..100];
            }

            if (!User.IsInRole("Admin"))
            {
                searchTerm = string.Empty;
            }

            sortBy = NormalizeSortBy(sortBy);

            sortDirection =
                NormalizeSortDirection(sortDirection);

            DateTime currentTime = DateTime.UtcNow;

            var baseQuery =
                GetAccessibleBillsQuery()
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

            // Include both bill types within the current user's access scope.
            // Split Pending monthly invoices by due date to avoid overlapping counts.
            var monthly = _monthlyBilling.Accessible(User).AsNoTracking();
            totalBillCount += await monthly.CountAsync();
            pendingBillCount += await monthly.CountAsync(x => x.Status == MonthlyInvoiceStatus.Pending && x.DueAtUtc >= currentTime);
            paidBillCount += await monthly.CountAsync(x => x.Status == MonthlyInvoiceStatus.Paid);
            overdueBillCount += await monthly.CountAsync(x => x.Status == MonthlyInvoiceStatus.Pending && x.DueAtUtc < currentTime);
            totalPendingAmount += await monthly.Where(x => x.Status == MonthlyInvoiceStatus.Pending)
                .SumAsync(x => (decimal?)x.Total) ?? 0m;

            var query =
                baseQuery.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(bill =>
                    bill.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(bill =>
                    bill.EnrollmentApplication.StudentName.Contains(searchTerm) ||
                    bill.EnrollmentApplication.GuardianName.Contains(searchTerm) ||
                    bill.EnrollmentApplication.GuardianEmail.Contains(searchTerm));
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

                            AcademicLevel =
                                bill.EnrollmentApplication
                                    .AcademicLevel,

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

                    SearchTerm =
                        searchTerm,

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
                await GetAccessibleBillsQuery()
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
                                    PaymentId =
                                        payment.PaymentId,

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

        private IQueryable<Bill>
            GetAccessibleBillsQuery()
        {
            IQueryable<Bill> query =
                _context.Bills;

            if (User.IsInRole("Admin"))
            {
                return query;
            }

            string? currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                return query.Where(bill => false);
            }

            if (User.IsInRole("Parent"))
            {
                return query.Where(bill =>
                    bill.EnrollmentApplication
                        .ExistingParentProfile != null &&
                    bill.EnrollmentApplication
                        .ExistingParentProfile
                        .ApplicationUserId ==
                    currentUserId);
            }

            if (User.IsInRole("Student"))
            {
                return query.Where(bill =>
                    bill.EnrollmentApplication
                        .ExistingStudentProfile != null &&
                    bill.EnrollmentApplication
                        .ExistingStudentProfile
                        .ApplicationUserId ==
                    currentUserId);
            }

            return query.Where(bill => false);
        }
    }
}