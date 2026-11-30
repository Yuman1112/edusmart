using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Billing;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class EnrollmentExitService(ApplicationDbContext db, ClassScheduleService schedules)
{
    public IQueryable<StudentEnrollment> Accessible(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin")) return db.StudentEnrollments;
        string? id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        bool student = user.IsInRole("Student"), parent = user.IsInRole("Parent");
        return db.StudentEnrollments.Where(e => (student && e.StudentProfile.ApplicationUserId == id) ||
            (parent && e.StudentProfile.ParentStudents.Any(p => p.IsActive && p.ParentProfile.ApplicationUserId == id)));
    }
    public static decimal TuitionRefund(decimal discountedTuition, decimal totalMonthlyFee, decimal classMonthlyFee, int paidMonths, int unusedMonths)
    {
        if (discountedTuition <= 0 || totalMonthlyFee <= 0 || classMonthlyFee <= 0 || paidMonths <= 0 || unusedMonths <= 0) return 0m;
        return decimal.Round(discountedTuition * Math.Min(classMonthlyFee, totalMonthlyFee) / totalMonthlyFee * Math.Min(unusedMonths, paidMonths) / paidMonths, 2, MidpointRounding.AwayFromZero);
    }
    public static Dictionary<int, decimal> AllocateTuition(decimal total, IEnumerable<(int ClassId, decimal Fee)> items)
    {
        var weights = items.GroupBy(x => x.ClassId).Select(g => (ClassId: g.Key, Fee: Math.Max(0m, g.Sum(x => x.Fee)))).ToList();
        decimal feeTotal = weights.Sum(x => x.Fee);
        if (total <= 0 || feeTotal <= 0) return weights.ToDictionary(x => x.ClassId, _ => 0m);
        long cents = checked((long)decimal.Round(total * 100m, 0, MidpointRounding.AwayFromZero));
        var raw = weights.Select(x => new { x.ClassId, Exact = cents * x.Fee / feeTotal }).ToList();
        var assigned = raw.ToDictionary(x => x.ClassId, x => checked((long)decimal.Floor(x.Exact)));
        long remainder = cents - assigned.Values.Sum();
        foreach (var part in raw.OrderByDescending(x => x.Exact - decimal.Floor(x.Exact)).ThenBy(x => x.ClassId).Take((int)remainder)) assigned[part.ClassId]++;
        return assigned.ToDictionary(x => x.Key, x => x.Value / 100m);
    }
    public async Task<(Payment? Payment, decimal Amount)> QuoteAsync(StudentEnrollment e, DateTime effective, int? excludeExitId = null)
    {
        if (!e.EnrollmentApplicationId.HasValue) return (null, 0m);
        var payment = await db.Payments.Include(p => p.Bill).ThenInclude(b => b.EnrollmentApplication).ThenInclude(a => a.SelectedSubjects)
            .Where(p => p.Bill.EnrollmentApplicationId == e.EnrollmentApplicationId && p.Status == PaymentStatus.Succeeded && p.PaymentProvider == "Stripe")
            .OrderBy(p => p.CompletedAt).FirstOrDefaultAsync();
        if (payment == null) return (null, 0m);
        var app = payment.Bill.EnrollmentApplication;
        var first = new DateTime(app.RequestedStartDate.Year, app.RequestedStartDate.Month, 1);
        var endExclusive = first.AddMonths(app.EnrollmentDurationMonths);
        var from = effective.Date; // The withdrawal date is fixed; delayed review must not consume the refund entitlement.
        if (from < first) from = first;
        int unused = Math.Max(0, (endExclusive.Year - from.Year) * 12 + endExclusive.Month - from.Month);
        var allocated = AllocateTuition(app.DiscountedTuitionAtApplication, app.SelectedSubjects.Select(x => (x.ClassSessionId, x.MonthlyFeeAtApplication)));
        decimal subjectTuition = allocated.GetValueOrDefault(e.ClassSessionId);
        decimal quote = TuitionRefund(subjectTuition, 1m, 1m, app.EnrollmentDurationMonths, unused);
        decimal committed = await db.EnrollmentExitRequests.Where(x => x.PaymentId == payment.PaymentId &&
            (!excludeExitId.HasValue || x.EnrollmentExitRequestId != excludeExitId.Value) &&
            (x.RefundStatus == ExitRefundStatus.Processing || x.RefundStatus == ExitRefundStatus.PendingProvider || x.RefundStatus == ExitRefundStatus.Completed))
            .SumAsync(x => (decimal?)x.ApprovedRefund) ?? 0m;
        return (payment, Math.Max(0m, Math.Min(quote, Math.Min(payment.Amount, payment.Bill.DiscountedTuitionAmount) - committed)));
    }
    public async Task<EnrollmentExitRequest> StopAsync(ClaimsPrincipal user, int enrollmentId, string reason, DateTime? expectedEffectiveDate = null, decimal? expectedRefund = null)
    {
        if (user.IsInRole("Admin") || (!user.IsInRole("Parent") && !user.IsInRole("Student")))
            throw new InvalidOperationException("Only the enrolled student or their linked parent can submit a withdrawal.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new InvalidOperationException("A withdrawal reason of up to 500 characters is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var e = await Accessible(user).Include(x => x.ClassSession).FirstOrDefaultAsync(x => x.StudentEnrollmentId == enrollmentId)
            ?? throw new InvalidOperationException("Enrollment not found or not accessible.");
        var existing = await db.EnrollmentExitRequests.FirstOrDefaultAsync(x => x.StudentEnrollmentId == enrollmentId);
        if (existing != null) { await tx.CommitAsync(); return existing; }
        if (e.Status != StudentEnrollmentStatus.Active) throw new InvalidOperationException("Only active enrollments can be withdrawn.");
        var effective = new DateTime(ClassScheduleService.Today.Year, ClassScheduleService.Today.Month, 1).AddMonths(1);
        var quote = await QuoteAsync(e, effective);
        if ((expectedEffectiveDate.HasValue && expectedEffectiveDate.Value != effective) ||
            (expectedRefund.HasValue && expectedRefund.Value != quote.Amount))
            throw new InvalidOperationException("The withdrawal date or refund estimate changed. Open the confirmation page again.");
        e.Status = StudentEnrollmentStatus.PendingDrop; e.DropRequestedAt = DateTime.UtcNow; e.DropEffectiveDate = effective; e.WithdrawalReason = reason.Trim();
        var request = new EnrollmentExitRequest { StudentEnrollmentId = enrollmentId, RequestedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Reason = reason.Trim(), EffectiveDate = effective, PaymentId = quote.Payment?.PaymentId, EstimatedRefund = quote.Amount,
            RefundStatus = quote.Amount > 0m ? ExitRefundStatus.PendingReview : ExitRefundStatus.NoRefundDue };
        db.EnrollmentExitRequests.Add(request);
        var recipients = await schedules.RecipientsAsync(new() { e.StudentProfileId }, e.ClassSession.TutorUserId);
        // A withdrawal must be visible immediately, including for a future-start enrollment.
        foreach (var recipient in recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            db.UserNotifications.Add(new SecureSmartTuitionSystem.Models.Communication.UserNotification {
                UserId = recipient, Title = "Withdrawal recorded", AvailableFromDate = ClassScheduleService.Today,
                Text = $"{e.ClassSession.ClassName}: regular enrollment stops from {effective:dd MMM yyyy}. Current-month bills and enrollment history are retained. Refunds, if applicable, require a separate Admin review." });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return request;
    }
}
