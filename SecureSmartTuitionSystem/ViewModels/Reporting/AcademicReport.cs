using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace SecureSmartTuitionSystem.ViewModels.Reporting;
public enum AcademicReportSection
{
    Combined,
    StudentPerformance,
    AssignmentReport
}

public sealed class AcademicReportFilter
{
    [Range(2000,2100)] public int Year { get; set; } = DateTime.UtcNow.AddHours(8).Year;
    [Range(1,int.MaxValue)] public int? SubjectId { get; set; }
    [Range(1,int.MaxValue)] public int? ClassId { get; set; }
}
public sealed record StudentPerformanceRow(int StudentId, string StudentNumber, string StudentName, int Expected, int Submitted, int Graded, decimal? Average);
public sealed record AssignmentRateRow(int Id, string Title, string ClassCode, string ClassName, string Subject, DateTime Due, int Expected, int Submitted, int OnTime, int Late, int Missing)
{
    public decimal? Rate => Expected == 0 ? null : 100m * Submitted / Expected;
}
public sealed class AcademicReport
{
    public AcademicReportFilter Filter { get; set; } = new();
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<SelectListItem> Classes { get; set; } = [];
    public List<StudentPerformanceRow> Students { get; set; } = [];
    public List<AssignmentRateRow> Assignments { get; set; } = [];
    public int Expected => Assignments.Sum(x=>x.Expected);
    public int Submitted => Assignments.Sum(x=>x.Submitted);
    public decimal? SubmissionRate => Expected == 0 ? null : 100m * Submitted / Expected;
    public int Graded { get; set; }
    public decimal? Average { get; set; }
}
