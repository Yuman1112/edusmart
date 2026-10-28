using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.ViewModels.Billing;

namespace SecureSmartTuitionSystem.Controllers;

// Read-only evidence of the initial enrollment bill's stated plan period.
// This is not an invoice generator or a decision that a month may be charged.
[Authorize(Roles = "Admin")]
public sealed class BillingCoverageController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? month, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest("Choose a valid month and page.");
        DateTime? selected = null;
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) || parsed.Year < 2000 || parsed.Year > 2100)
                return BadRequest("Choose a month between 2000-01 and 2100-12.");
            selected = parsed;
        }
        // Default to all linked plans, including paid plans starting next month.
        // Payment date does not decide which tuition months a plan covers.
        var query = db.StudentEnrollments.AsNoTracking()
            .Where(e => e.EnrollmentApplicationId != null && e.EnrollmentApplication != null &&
                e.EnrollmentApplication.EnrollmentDurationMonths > 0 && e.EnrollmentApplication.EnrollmentDurationMonths <= 12);
        if (selected.HasValue)
        {
            int selectedIndex = selected.Value.Year * 12 + selected.Value.Month;
            query = query.Where(e =>
                selectedIndex >= e.EnrollmentApplication!.RequestedStartDate.Year * 12 + e.EnrollmentApplication.RequestedStartDate.Month &&
                selectedIndex < e.EnrollmentApplication.RequestedStartDate.Year * 12 + e.EnrollmentApplication.RequestedStartDate.Month + e.EnrollmentApplication.EnrollmentDurationMonths);
        }
        int count = await query.CountAsync(cancellationToken);
        int pages = Math.Max(1, (int)Math.Ceiling(count / 25d));
        page = Math.Clamp(page, 1, pages);
        var enrollments = await query.OrderBy(e => e.StudentProfile.StudentNumber).ThenBy(e => e.StudentEnrollmentId)
            .Skip((page - 1) * 25).Take(25)
            .Select(e => new { e.StudentEnrollmentId, e.EnrollmentApplicationId,
                Student = e.StudentProfile.FullName, StudentNumber = e.StudentProfile.StudentNumber,
                Class = e.ClassSession.ClassName ?? e.ClassSession.ClassCode,
                e.Status, e.DropEffectiveDate, e.EndedAt,
                Start = e.EnrollmentApplication!.RequestedStartDate,
                Months = e.EnrollmentApplication.EnrollmentDurationMonths,
                e.EnrollmentApplication.ApplicationNumber }).ToListAsync(cancellationToken);
        var applicationIds = enrollments.Select(e => e.EnrollmentApplicationId!.Value).Distinct().ToList();
        var bills = await db.Bills.AsNoTracking().Where(b => applicationIds.Contains(b.EnrollmentApplicationId))
            .Select(b => new { b.EnrollmentApplicationId, b.BillNumber, b.Status,
                HasRefundedPayment = b.Payments.Any(p => p.Status == PaymentStatus.Refunded),
                HasExit = db.EnrollmentExitRequests.Any(x => x.StudentEnrollment.EnrollmentApplicationId == b.EnrollmentApplicationId) })
            .ToListAsync(cancellationToken);
        var byApplication = bills.ToLookup(b => b.EnrollmentApplicationId);
        var rows = enrollments.Select(e => {
            var initial = byApplication[e.EnrollmentApplicationId!.Value].ToList();
            var firstMonth = new DateTime(e.Start.Year, e.Start.Month, 1);
            var endMonth = firstMonth.AddMonths(e.Months - 1);
            return new BillingCoverageRow {
                Student = e.Student, StudentNumber = e.StudentNumber, Class = e.Class,
                ApplicationNumber = e.ApplicationNumber, StartMonth = firstMonth, EndMonth = endMonth,
                EnrollmentStatus = e.Status.ToString(), DropEffectiveDate = e.DropEffectiveDate, EndedAt = e.EndedAt,
                BillNumber = initial.Count == 1 ? initial[0].BillNumber : null,
                BillStatus = initial.Count == 1 ? initial[0].Status.ToString() : "Review: missing or multiple bills",
                NeedsReview = initial.Count != 1 || initial.Any(b => b.HasRefundedPayment || b.HasExit || b.Status == BillStatus.Refunded || b.Status == BillStatus.Cancelled)
            };
        }).ToList();
        return View(new BillingCoverageViewModel { Month = selected?.ToString("yyyy-MM", CultureInfo.InvariantCulture) ?? "", Page = page, TotalPages = pages, TotalRecords = count, Rows = rows });
    }
}
