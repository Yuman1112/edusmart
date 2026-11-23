using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace SecureSmartTuitionSystem.ViewModels.Reporting;
public enum AcademicReportSection
{
    Combined,
    StudentPerformance,
    AssignmentReport
}

public sealed class AcademicReportFilter : ReportPeriodFilter
{
}
public sealed record StudentPerformanceRow(int StudentId, string StudentNumber, string StudentName, int Expected, int Submitted, int Graded, decimal? Average, string Level = "");
public sealed record AssignmentRateRow(int Id, string Title, string ClassCode, string ClassName, string Subject, DateTime Due, int Expected, int Submitted, int OnTime, int Late, int Missing, string Schedule = "")
{
    public decimal? Rate => Expected == 0 ? null : 100m * Submitted / Expected;
}
public sealed class AcademicReport
{
    public AcademicReportFilter Filter { get; set; } = new();
    public ReportClassOptions ClassOptions { get; } = new();
    public List<StudentPerformanceRow> Students { get; set; } = [];
    public List<StudentClassPerformance> StudentClasses { get; set; } = [];
    public List<AssignmentRateRow> Assignments { get; set; } = [];
    public int Expected => Assignments.Sum(x=>x.Expected);
    public int Submitted => Assignments.Sum(x=>x.Submitted);
    public decimal? SubmissionRate => Expected == 0 ? null : 100m * Submitted / Expected;
    public int Graded { get; set; }
    public decimal? Average { get; set; }
}

public sealed record StudentClassPerformance(string ClassName, string Schedule, StudentPerformanceRow Summary, IReadOnlyList<StudentAssessmentDetails> Assessments);
public sealed record StudentPerformanceDetails(AcademicReportFilter Filter, StudentPerformanceRow Student,
    IReadOnlyList<StudentClassPerformance> Classes);

public sealed record StudentAssessmentDetails(string Title, string Kind, DateTime DueMyt,
    DateTime? SubmittedMyt, decimal? Marks, decimal MaxMarks, bool ResultsReleased, string? Feedback);
