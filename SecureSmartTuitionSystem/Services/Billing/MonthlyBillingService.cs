using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Billing;
public sealed class MonthlyBillingService(ApplicationDbContext db)
{
    public static DateTime MalaysiaToday => DateTime.UtcNow.AddHours(8).Date;
    public static DateTime MonthOf(DateTime date) => new(date.Year, date.Month, 1);
    public static DateTime DueAt(DateTime month, DateTime? issuedAtUtc = null)
    {
        var regular = MonthOf(month).AddDays(7).AddHours(-8);
        // A bill first created after the normal issue date must not instantly
        // make an existing family overdue. Allow seven local calendar days.
        var actualIssueDay = (issuedAtUtc ?? DateTime.UtcNow).AddHours(8).Date;
        var latest = actualIssueDay.AddDays(7).AddHours(-8);
        return regular > latest ? regular : latest;
    }
    public static bool IsRestricted(MonthlyInvoice invoice, DateTime nowUtc) =>
        invoice.Status == MonthlyInvoiceStatus.Pending && invoice.Total > 0 && nowUtc >= invoice.DueAtUtc.AddDays(7);
    public IQueryable<MonthlyInvoice> Accessible(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.IsInRole("Admin")) return db.Set<MonthlyInvoice>();
        if (user.IsInRole("Parent")) return db.Set<MonthlyInvoice>().Where(x =>
            x.StudentProfile.ParentStudents.Any(p => p.IsActive && p.ParentProfile.ApplicationUserId == id));
        if (user.IsInRole("Student")) return db.Set<MonthlyInvoice>().Where(x => x.StudentProfile.ApplicationUserId == id);
        return db.Set<MonthlyInvoice>().Where(x => false);
    }
    public async Task<bool> HasRestrictionAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!user.IsInRole("Parent") && !user.IsInRole("Student") || user.IsInRole("Admin")) return false;
        var cutoff = DateTime.UtcNow.AddDays(-7);
        return await Accessible(user).AnyAsync(x => x.Status == MonthlyInvoiceStatus.Pending && x.Total > 0 && x.DueAtUtc <= cutoff, ct);
    }
    public Task<List<SecureSmartTuitionSystem.ViewModels.Billing.MonthlyEmailStatus>> EmailStatusAsync(int invoiceId, CancellationToken ct) =>
        db.Set<BillingReminder>().AsNoTracking().Where(r => r.MonthlyInvoiceId == invoiceId && r.Kind == "overdue-restricted" &&
            db.ParentStudents.Any(p => p.StudentProfileId == r.MonthlyInvoice.StudentProfileId && p.IsActive && p.ParentProfile.ApplicationUserId == r.UserId))
            .OrderByDescending(r => r.Day).ThenByDescending(r => r.BillingReminderId).Take(25)
            .Select(r => new SecureSmartTuitionSystem.ViewModels.Billing.MonthlyEmailStatus {
                Recipient = db.Users.Where(u => u.Id == r.UserId).Select(u => u.FullName ?? u.UserName ?? "Guardian").FirstOrDefault() ?? "Guardian",
                Day = r.Day, Status = r.EmailSentAtUtc != null ? "Sent" : r.EmailError != null ? "Unconfirmed" : r.EmailAttemptedAtUtc != null ? "Attempted" : "Queued",
                Note = r.EmailError }).ToListAsync(ct);
    public static bool Eligible(StudentEnrollment e, DateTime month)
    {
        month = MonthOf(month);
        var next = month.AddMonths(1);
        // This system's enrollments end at the end of their academic year.
        return e.ClassSession.Status == ClassSessionStatus.Active && e.StartDate.Year == month.Year && e.StartDate < next &&
            (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > month) &&
            (!e.EndedAt.HasValue || e.EndedAt > month) &&
            (!e.ClassSession.StartDate.HasValue || e.ClassSession.StartDate < next) &&
            (!e.ClassSession.EndDate.HasValue || e.ClassSession.EndDate >= month) &&
            (e.Status != StudentEnrollmentStatus.Dropped || e.DropEffectiveDate > month) &&
            (e.Status != StudentEnrollmentStatus.Completed || e.EndedAt > month);
    }
    public static bool Prepaid(StudentEnrollment e, DateTime month, bool initialPaid)
    {
        var app = e.EnrollmentApplication;
        return initialPaid && app != null && app.EnrollmentDurationMonths is >= 1 and <= 12 &&
            MonthOf(month) >= MonthOf(app.RequestedStartDate) &&
            MonthOf(month) < MonthOf(app.RequestedStartDate).AddMonths(app.EnrollmentDurationMonths);
    }
    // Called on the first day, retried while the app is running. A restart still
    // processes the current month. No historical debt is silently invented.
    public async Task<int> GenerateCurrentAsync(CancellationToken ct = default)
    {
        var month = MonthOf(MalaysiaToday);
        var students = await db.StudentEnrollments.AsNoTracking().Where(e =>
            e.StartDate.Year == month.Year && e.StartDate < month.AddMonths(1))
            .Select(e => e.StudentProfileId).Distinct().ToListAsync(ct);
        int count = 0;
        foreach (var studentId in students)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.Set<MonthlyInvoice>().AnyAsync(x => x.StudentProfileId == studentId && x.Month == month, ct))
            { await tx.CommitAsync(ct); continue; }
            var enrollments = await db.StudentEnrollments.Include(x => x.ClassSession).Include(x => x.EnrollmentApplication)
                .Where(x => x.StudentProfileId == studentId).ToListAsync(ct);
            var appIds = enrollments.Where(e => e.EnrollmentApplicationId.HasValue).Select(e => e.EnrollmentApplicationId!.Value).Distinct().ToArray();
            var paidApps = await db.Bills.Where(b => appIds.Contains(b.EnrollmentApplicationId) && b.Status == BillStatus.Paid)
                .Select(b => b.EnrollmentApplicationId).ToListAsync(ct);
            // Never charge legacy enrollments without a verifiable original bill.
            // An admin must resolve those records before they enter recurring billing.
            var eligible = enrollments.Where(e => Eligible(e, month) && e.EnrollmentApplicationId.HasValue &&
                    paidApps.Contains(e.EnrollmentApplicationId.Value)).ToList();
            var lines = eligible.GroupBy(e => e.ClassSessionId).Where(g => !g.Any(e => Prepaid(e, month, true)))
                .Select(g => g.OrderByDescending(e => e.StartDate).ThenByDescending(e => e.StudentEnrollmentId).First())
                .Select(e => new MonthlyInvoiceLine { StudentEnrollmentId = e.StudentEnrollmentId,
                    Description = e.ClassSession.ClassName ?? e.ClassSession.ClassCode,
                    Amount = decimal.Round(e.MonthlyFeeAtEnrollment, 2, MidpointRounding.AwayFromZero) }).ToList();
            if (lines.Count == 0) { await tx.CommitAsync(ct); continue; }
            if (lines.Any(x => x.Amount < 0) || lines.Sum(x => x.Amount) > 99999999.99m)
                throw new InvalidOperationException("Invalid recurring tuition snapshot; review the student record.");
            decimal total = lines.Sum(x => x.Amount);
            var issuedAt = DateTime.UtcNow;
            db.Add(new MonthlyInvoice { StudentProfileId = studentId, Month = month, IssuedAtUtc = issuedAt,
                DueAtUtc = DueAt(month, issuedAt), Total = total, Lines = lines,
                Status = total == 0 ? MonthlyInvoiceStatus.Paid : MonthlyInvoiceStatus.Pending,
                PaidAtUtc = total == 0 ? DateTime.UtcNow : null });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); count++;
        }
        return count;
    }
}
