using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<MonthlyIncomePoint>> GetMonthlyIncomeAsync(int months = 6)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);

        // OLAP-style aggregation: GROUP BY month over the Payments/Invoices tables
        // (Ch.2.4: "SQL aggregation queries using GROUP BY and ROLLUP are executed
        // directly against the related Enrollment, Payment, and Assignment tables").
        // Expressed here as LINQ so EF Core parameterises and translates it directly
        // to a single, indexed SQL Server query (no raw string ever built).
        var grouped = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.ProcessedAt != null && p.ProcessedAt >= cutoff)
            .GroupBy(p => new { p.ProcessedAt!.Value.Year, p.ProcessedAt!.Value.Month })
            .Select(g => new MonthlyIncomePoint(g.Key.Year, g.Key.Month, g.Sum(p => p.AmountPaid)))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToListAsync();

        return grouped;
    }

    public async Task<SubmissionRateResult> GetSubmissionRateAsync(int? subjectId = null, string? formYear = null)
    {
        var query = _db.AssignmentSubmissions
            .Include(s => s.Assignment).ThenInclude(a => a.ClassSession)
            .Include(s => s.Student)
            .AsQueryable();

        if (subjectId is not null)
            query = query.Where(s => s.Assignment.ClassSession.SubjectId == subjectId);

        if (!string.IsNullOrWhiteSpace(formYear))
            query = query.Where(s => s.Student.FormYear == formYear);

        var now = DateTime.UtcNow;

        var onTime = await query.CountAsync(s => s.SubmittedAt != null && !s.IsLate);
        var late = await query.CountAsync(s => s.SubmittedAt != null && s.IsLate);
        var missing = await query.CountAsync(s => s.SubmittedAt == null && s.Assignment.DueDate < now);

        return new SubmissionRateResult(onTime, late, missing);
    }

    public async Task<List<StudentPerformanceResult>> GetStudentPerformanceSummaryAsync(int? subjectId = null, string? formYear = null)
    {
        var query = _db.AssignmentSubmissions
            .Where(s => s.Score != null)
            .Include(s => s.Assignment).ThenInclude(a => a.ClassSession).ThenInclude(c => c.Subject)
            .Include(s => s.Student)
            .AsQueryable();

        if (subjectId is not null)
            query = query.Where(s => s.Assignment.ClassSession.SubjectId == subjectId);

        if (!string.IsNullOrWhiteSpace(formYear))
            query = query.Where(s => s.Student.FormYear == formYear);

        var results = await query
            .GroupBy(s => new { s.Assignment.ClassSession.Subject.SubjectName, s.Student.FormYear })
            .Select(g => new StudentPerformanceResult(
                g.Key.SubjectName,
                g.Key.FormYear,
                Math.Round(g.Average(s => s.Score!.Value), 1),
                g.Count()))
            .ToListAsync();

        return results;
    }

    public async Task<List<ClassProgressResult>> GetClassProgressOverviewAsync(int? classSessionId = null)
    {
        var query = _db.ClassSessions.Include(c => c.Subject).AsQueryable();
        if (classSessionId is not null)
            query = query.Where(c => c.ClassSessionId == classSessionId);

        var sessions = await query.ToListAsync();
        var results = new List<ClassProgressResult>();

        foreach (var session in sessions)
        {
            var assignments = await _db.Assignments
                .Where(a => a.ClassSessionId == session.ClassSessionId)
                .ToListAsync();

            var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();

            var submissions = await _db.AssignmentSubmissions
                .Where(s => assignmentIds.Contains(s.AssignmentId))
                .ToListAsync();

            var submittedCount = submissions.Count(s => s.SubmittedAt != null);
            var expectedCount = assignments.Count == 0 ? 0 : submissions.Count;
            var rate = expectedCount == 0 ? 0m : Math.Round(100m * submittedCount / expectedCount, 1);

            results.Add(new ClassProgressResult(
                session.ClassSessionId,
                session.Subject.SubjectName,
                assignments.Count,
                rate));
        }

        return results;
    }

    public async Task<IncomeReportResult> GetIncomeReportAsync(DateOnly? periodStart = null, DateOnly? periodEnd = null)
    {
        // Demonstrates a raw T-SQL GROUP BY ... WITH ROLLUP query (Ch.2.4),
        // executed safely via a parameterised FromSqlInterpolated call rather
        // than string concatenation (Ch.2.5 "Parameterised Queries").
        var start = periodStart ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12));
        var end = periodEnd ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var monthlyTotals = await _db.Database
            .SqlQuery<MonthlyIncomeRow>($@"
                SELECT
                    YEAR(p.ProcessedAt)  AS Year,
                    MONTH(p.ProcessedAt) AS Month,
                    SUM(p.AmountPaid)    AS TotalIncome
                FROM dbo.Payments p
                WHERE p.Status = 'Success'
                  AND p.ProcessedAt IS NOT NULL
                  AND CAST(p.ProcessedAt AS date) BETWEEN {start.ToDateTime(TimeOnly.MinValue)} AND {end.ToDateTime(TimeOnly.MaxValue)}
                GROUP BY YEAR(p.ProcessedAt), MONTH(p.ProcessedAt) WITH ROLLUP
                ORDER BY Year, Month")
            .ToListAsync();

        // The ROLLUP super-aggregate row has NULL Year/Month - that is the grand total.
        var grandTotalRow = monthlyTotals.FirstOrDefault(r => r.Year == null);
        var byMonth = monthlyTotals
            .Where(r => r.Year != null && r.Month != null)
            .Select(r => new MonthlyIncomePoint(r.Year!.Value, r.Month!.Value, r.TotalIncome))
            .ToList();

        var totalIncome = grandTotalRow?.TotalIncome ?? byMonth.Sum(m => m.TotalIncome);

        var outstandingBalance = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.PendingPayment || i.Status == InvoiceStatus.Overdue)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        return new IncomeReportResult(totalIncome, outstandingBalance, byMonth);
    }

    /// <summary>Row shape for the raw ROLLUP query above (nullable Year/Month = the ROLLUP grand-total row).</summary>
    private class MonthlyIncomeRow
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public decimal TotalIncome { get; set; }
    }
}
