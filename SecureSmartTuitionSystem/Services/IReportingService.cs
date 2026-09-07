namespace SecureSmartTuitionSystem.Services;

public record MonthlyIncomePoint(int Year, int Month, decimal TotalIncome);

public record SubmissionRateResult(int OnTime, int Late, int Missing)
{
    public int Total => OnTime + Late + Missing;
    public decimal OnTimePct => Total == 0 ? 0 : Math.Round(100m * OnTime / Total, 1);
    public decimal LatePct => Total == 0 ? 0 : Math.Round(100m * Late / Total, 1);
    public decimal MissingPct => Total == 0 ? 0 : Math.Round(100m * Missing / Total, 1);
}

public record StudentPerformanceResult(string Subject, string FormYear, decimal AverageScore, int GradedSubmissionCount);

public record ClassProgressResult(int ClassSessionId, string SubjectName, int AssignmentsSet, decimal SubmissionRatePct);

public record IncomeReportResult(decimal TotalIncome, decimal OutstandingBalance, List<MonthlyIncomePoint> ByMonth);

/// <summary>
/// OLAP-style aggregation applied directly against the operational SQL
/// Server tables (Ch.2.4 "Adopted Approach and Implementation", Reporting
/// &amp; Analytics Module) — GROUP BY / ROLLUP / filtered subqueries rather
/// than a separate data warehouse, per Chapter 2's comparison of reporting
/// techniques.
/// </summary>
public interface IReportingService
{
    /// <summary>Monthly income bar chart, most recent N months (Functional Requirements: Reporting and Analytics).</summary>
    Task<List<MonthlyIncomePoint>> GetMonthlyIncomeAsync(int months = 6);

    /// <summary>Assignment submission rate donut chart: on-time / late / missing (Functional Requirements; Table 4.23).</summary>
    Task<SubmissionRateResult> GetSubmissionRateAsync(int? subjectId = null, string? formYear = null);

    /// <summary>Average scores by subject/level (Table 4.22). Attendance-rate figures are owned by the Attendance Management module and are out of this report's scope.</summary>
    Task<List<StudentPerformanceResult>> GetStudentPerformanceSummaryAsync(int? subjectId = null, string? formYear = null);

    /// <summary>Materials posted / assignments set / submission rate per class (Table 4.25). Materials-posted count is owned by the Learning & Assessment module and is out of this report's scope (returned as 0).</summary>
    Task<List<ClassProgressResult>> GetClassProgressOverviewAsync(int? classSessionId = null);

    /// <summary>Total income + outstanding balances, optionally filtered by billing period (Table 4.24).</summary>
    Task<IncomeReportResult> GetIncomeReportAsync(DateOnly? periodStart = null, DateOnly? periodEnd = null);
}
