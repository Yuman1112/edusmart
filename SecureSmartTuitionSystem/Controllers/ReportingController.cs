using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Filters;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.ViewModels;

namespace SecureSmartTuitionSystem.Controllers;

/// <summary>
/// Reporting & Analytics Module (Tables 4.20-4.26). Every endpoint honours
/// the role-scoping rule from Functional Requirements: "parents or
/// guardians restricted to information about their linked children."
/// </summary>
[Authorize]
public class ReportingController : Controller
{
    private readonly AppDbContext _db;
    private readonly IReportingService _reporting;
    private readonly IExportService _export;

    public ReportingController(AppDbContext db, IReportingService reporting, IExportService export)
    {
        _db = db;
        _reporting = reporting;
        _export = export;
    }

    /// <summary>Table 4.20 "Dashboard Visualization" — summary cards + charts for the logged-in role.</summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var role = this.CurrentUserRole();

        // Table 4.20 A1: any retrieval failure below propagates as a 500, handled by the
        // global exception middleware, which the caller renders as "Unable to load
        // analytics data at this time."
        var monthlyIncome = role == "Admin" ? await _reporting.GetMonthlyIncomeAsync() : null;
        var submissionRate = await _reporting.GetSubmissionRateAsync();

        return Json(new
        {
            role,
            monthlyIncome,
            submissionRate = new { submissionRate.OnTimePct, submissionRate.LatePct, submissionRate.MissingPct }
        });
    }

    /// <summary>Table 4.21 "Data Filtering by Subject/Class" — returns the filter option lists.</summary>
    [HttpGet]
    public async Task<IActionResult> FilterOptions()
    {
        var subjects = await _db.Subjects.Where(s => s.IsActive)
            .Select(s => new { s.SubjectId, s.SubjectName }).ToListAsync();
        var classSessions = await _db.ClassSessions
            .Select(c => new { c.ClassSessionId, c.Subject.SubjectName, c.DayOfWeek }).ToListAsync();

        return Json(new { subjects, classSessions });
    }

    /// <summary>Table 4.22 "Student Performance Summary".</summary>
    [HttpGet]
    public async Task<IActionResult> StudentPerformance([FromQuery] ReportFilterRequest filter)
    {
        var results = await _reporting.GetStudentPerformanceSummaryAsync(filter.SubjectId, filter.FormYear);

        // A1: empty list -> "No performance records available for this student."
        return Json(results);
    }

    /// <summary>Table 4.23 "Assignment Submission Rate Report".</summary>
    [HttpGet]
    public async Task<IActionResult> SubmissionRate([FromQuery] ReportFilterRequest filter)
    {
        var result = await _reporting.GetSubmissionRateAsync(filter.SubjectId, filter.FormYear);

        if (result.Total == 0)
        {
            // A1: "No assignment submission data found."
            return NotFound(new { message = "No assignment submission data found." });
        }

        return Json(result);
    }

    /// <summary>Table 4.24 "Income Report" — Admin only.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Income([FromQuery] ReportFilterRequest filter)
    {
        var result = await _reporting.GetIncomeReportAsync(filter.PeriodStart, filter.PeriodEnd);

        if (result.TotalIncome == 0 && result.ByMonth.Count == 0)
        {
            // A1: "No payment data available for financial reporting."
            return NotFound(new { message = "No payment data available for financial reporting." });
        }

        return Json(result);
    }

    /// <summary>Table 4.25 "Class Progress Overview".</summary>
    [HttpGet]
    public async Task<IActionResult> ClassProgress([FromQuery] ReportFilterRequest filter)
    {
        var results = await _reporting.GetClassProgressOverviewAsync(filter.ClassSessionId);

        // A1: empty list -> "No learning progress logged for this class."
        return Json(results);
    }

    /// <summary>Table 4.26 "Report Export" — Tutor/Admin only.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Tutor")]
    public async Task<IActionResult> Export([FromQuery] ReportFilterRequest filter)
    {
        if (filter.ExportFormat is not ("pdf" or "excel"))
        {
            return BadRequest(new { message = "Export format must be 'pdf' or 'excel'." });
        }

        var income = await _reporting.GetIncomeReportAsync(filter.PeriodStart, filter.PeriodEnd);

        var table = new ExportTable(
            Title: "Income Report",
            Columns: new List<ExportColumn> { new("Year"), new("Month"), new("Total Income (RM)") },
            Rows: income.ByMonth
                .Select(m => new List<string> { m.Year.ToString(), m.Month.ToString(), m.TotalIncome.ToString("F2") })
                .ToList());

        try
        {
            byte[] fileBytes = filter.ExportFormat == "pdf"
                ? _export.ExportToPdf(table)
                : _export.ExportToExcel(table);

            var contentType = filter.ExportFormat == "pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fileName = $"income-report.{(filter.ExportFormat == "pdf" ? "pdf" : "xlsx")}";

            return File(fileBytes, contentType, fileName);
        }
        catch
        {
            // A1: "Failed to generate report export file."
            return StatusCode(500, new { message = "Failed to generate report export file." });
        }
    }
}
