using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Services.Communication;
public sealed class ChatbotContextService(ApplicationDbContext db, MonthlyBillingService monthly)
{
    public static readonly string[] Scopes = ["auto", "centre", "available", "bills", "enrollments"];
    public static string ResolveScope(string question, string scope)
    {
        if (scope != "auto") return scope;
        var q = question.ToLowerInvariant();
        bool own = q.Contains("my ") || q.Contains("our ") || q.Contains("我的") || q.Contains("我孩子") || q.Contains("我小孩") || q.Contains("我欠") || q.Contains("我报") || q.Contains("我选") || q.Contains("i owe");
        if (Regex.IsMatch(question, @"\b(?:BL|MB|EA)\d+\b", RegexOptions.IgnoreCase)) return "bills";
        if (own && new[]{"bill","paid","payment","receipt","owe","outstanding","balance","账单","付款","缴费","收据","欠","余额"}.Any(q.Contains)) return "bills";
        if (own && new[]{"class","enrol","subject","课程","班级","科目","课表"}.Any(q.Contains)) return "enrollments";
        return new[]{"available","seat","form ","year ","primary","secondary","有位","空位","小学","中学","有什么班"}.Any(q.Contains) ? "available" : "centre";
    }
    public IQueryable<Bill> FamilyBills(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var q = db.Bills.AsNoTracking();
        if (string.IsNullOrEmpty(id)) return q.Where(x => false);
        if (user.IsInRole("Parent")) return q.Where(x => x.EnrollmentApplication.ExistingParentProfile != null && x.EnrollmentApplication.ExistingParentProfile.ApplicationUserId == id);
        if (user.IsInRole("Student")) return q.Where(x => x.EnrollmentApplication.ExistingStudentProfile != null && x.EnrollmentApplication.ExistingStudentProfile.ApplicationUserId == id);
        return q.Where(x => false);
    }
    public IQueryable<StudentEnrollment> FamilyEnrollments(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var q = db.StudentEnrollments.AsNoTracking();
        if (string.IsNullOrEmpty(id)) return q.Where(x => false);
        if (user.IsInRole("Parent")) return q.Where(x => x.StudentProfile.ParentStudents.Any(p => p.IsActive && p.ParentProfile.ApplicationUserId == id));
        if (user.IsInRole("Student")) return q.Where(x => x.StudentProfile.ApplicationUserId == id);
        return q.Where(x => false);
    }
    public async Task<List<ChatbotSource>> GetAsync(ClaimsPrincipal user, string scope, string question, CancellationToken ct)
    {
        var rows = new List<ChatbotSource>();
        if (scope is "bills" or "enrollments" && (!user.IsInRole("Parent") && !user.IsInRole("Student"))) return rows;
        if (scope == "bills")
        {
            decimal originalOutstanding = await FamilyBills(user).Where(x => x.Currency == "MYR" && (x.Status == BillStatus.PendingPayment || x.Status == BillStatus.Overdue)).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
            decimal monthlyOutstanding = await monthly.Accessible(user).AsNoTracking().Where(x => x.Status == MonthlyInvoiceStatus.Pending).SumAsync(x => (decimal?)x.Total, ct) ?? 0m;
            rows.Add(new("balance-summary", "Current outstanding amount", "Outstanding across all permitted unpaid MYR enrollment and monthly bills: MYR " + (originalOutstanding + monthlyOutstanding).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ". This is a server-calculated balance; do not sum the limited record preview as the total.", "/Billing"));
            var originals = await FamilyBills(user).OrderByDescending(x => x.IssuedAt).Take(10)
                .Select(x => new { x.BillId, x.BillNumber, x.TotalAmount, x.Currency, x.Status, x.DueAt }).ToListAsync(ct);
            foreach (var b in originals) rows.Add(new("bill-" + b.BillId, b.BillNumber,
                $"Enrollment bill {b.BillNumber}: {b.Currency} {b.TotalAmount:F2}; status {b.Status}; due {b.DueAt.AddHours(8):dd MMM yyyy HH:mm} MYT.", "/Billing/Details/" + b.BillId));
            // Do not allow Admin's broad MonthlyBilling access through a family chatbot tool.
            var recurring = await monthly.Accessible(user).AsNoTracking().OrderByDescending(x => x.Month).Take(10).ToListAsync(ct);
            foreach (var b in recurring) rows.Add(new("monthly-" + b.MonthlyInvoiceId, b.Number,
                $"Monthly bill {b.Number}, tuition {b.Month:MMM yyyy}: MYR {b.Total:F2}; status {(b.Status == MonthlyInvoiceStatus.Pending && b.DueAtUtc < DateTime.UtcNow ? "Overdue" : b.Status.ToString())}; due {b.DueAtUtc.AddHours(8).AddSeconds(-1):dd MMM yyyy HH:mm} MYT.", "/MonthlyBilling/Details/" + b.MonthlyInvoiceId));
            rows.Add(new("bills-scope", "Account query scope", "Only this signed-in user's permitted family records were queried. Up to 10 latest enrollment bills and 10 latest monthly bills are listed; the complete outstanding amount is in balance-summary, not a total of this preview. Open the billing pages for complete history.", "/Billing"));
        }
        if (scope == "enrollments")
        {
            var records = await FamilyEnrollments(user).OrderByDescending(x => x.StartDate).Take(20)
                .Select(x => new { x.StudentEnrollmentId, x.StudentProfile.StudentNumber, x.ClassSession.ClassName, x.StartDate, x.Status, x.DropEffectiveDate }).ToListAsync(ct);
            foreach (var e in records) rows.Add(new("enrollment-" + e.StudentEnrollmentId, e.StudentNumber,
                $"{e.StudentNumber}: {e.ClassName}; starts {e.StartDate:dd MMM yyyy}; recorded status {e.Status}; withdrawal effective {(e.DropEffectiveDate.HasValue ? e.DropEffectiveDate.Value.ToString("dd MMM yyyy") : "not scheduled")}.", "/EnrollmentHistory"));
            rows.Add(new("enrollments-scope", "Account query scope", "Only linked family enrollments were queried, latest 20. A future start date does not mean the class is already available in My Classes.", "/EnrollmentHistory"));
        }
        if (scope == "available")
        {
            var today = ClassScheduleService.Today;
            var q = db.ClassSessions.AsNoTracking().Where(c => c.Status == ClassSessionStatus.Active && c.Subject.IsActive &&
                (!c.EndDate.HasValue || c.EndDate >= today) && (c.StartDate ?? c.CreatedAt.AddHours(8)).Year >= today.Year);
            var match = Regex.Match(question, @"\b(YEAR\s+[1-6]|FORM\s+[1-5])\b", RegexOptions.IgnoreCase);
            if (match.Success) { var level = Regex.Replace(match.Value.ToUpperInvariant(), @"\s+", " "); q = q.Where(c => c.Subject.AcademicLevel == level); }
            var classes = await q.Select(c => new { c.ClassSessionId, c.ClassCode, c.ClassName, c.Subject.AcademicLevel, c.Subject.SubjectName, c.Subject.MonthlyFee,
                c.DayOfWeek, c.StartTime, c.StartDate, c.Capacity,
                Occupied = db.StudentEnrollments.Count(e => e.ClassSessionId == c.ClassSessionId && e.StartDate.Year >= today.Year &&
                    (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop) &&
                    (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > today) && (!e.EndedAt.HasValue || e.EndedAt > today)) +
                    db.Set<EnrollmentApplicationSubject>().Count(x => x.ClassSessionId == c.ClassSessionId &&
                        (x.EnrollmentApplication.Status == EnrollmentStatus.ApprovedPendingPayment || x.EnrollmentApplication.Status == EnrollmentStatus.PaymentConfirmed))
            }).Where(x => x.Occupied < x.Capacity).OrderBy(x => x.AcademicLevel).ThenBy(x => x.SubjectName).Take(20).ToListAsync(ct);
            foreach (var c in classes) rows.Add(new("class-" + c.ClassSessionId, c.ClassCode,
                $"{c.ClassCode}: {SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(c.ClassName)}; level {c.AcademicLevel}; subject {c.SubjectName}; {c.DayOfWeek} {c.StartTime:hh:mm tt}; listed monthly tuition MYR {c.MonthlyFee:F2}; seats {c.Capacity-c.Occupied}; start {(c.StartDate.HasValue ? c.StartDate.Value.ToString("dd MMM yyyy") : "check class page")}.", "/Classes"));
            rows.Add(new("classes-scope", "Availability query scope", "This is a live preview of up to 20 available classes, optionally filtered by a YEAR or FORM number in the question. It is not a full catalogue or a seat reservation. Start-date eligibility and final fees are validated during enrollment.", "/Classes"));
        }
        return rows;
    }
}
