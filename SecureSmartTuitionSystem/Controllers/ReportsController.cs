using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Reporting;
using SecureSmartTuitionSystem.ViewModels.Reporting;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ApplicationDbContext db, SecurityEventService securityEvents) : Controller
{
    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(string? end, int? months, CancellationToken ct, int? year = null, int? month = null)
    {
        if (!ModelState.IsValid || (months ?? 1) is < 1 or > 12) return BadRequest("Choose a period from 1 to 12 months.");
        if (year.HasValue || month.HasValue)
        {
            var now = DateTime.UtcNow.AddHours(8);
            if (!year.HasValue || !month.HasValue || year < 2000 || year > now.Year || month < 1 || month > 12)
                return BadRequest("Choose a valid year and month.");
            // When switching to the current year, keep the latest available month.
            var selectedMonth = year == now.Year ? Math.Min(month.Value, now.Month) : month.Value;
            end = $"{year.Value:D4}-{selectedMonth:D2}";
        }
        if (!IncomeReportService.TryEndMonth(end, DateTime.UtcNow, out var endMonth)) return BadRequest("Choose a valid month from 2000 through the current month.");
        return View(await new IncomeReportService(db).BuildAsync(endMonth, months ?? 1, ct));
    }

    [HttpGet]
    public Task<IActionResult> Academic([FromQuery] AcademicReportFilter filter, CancellationToken ct) =>
        BuildAcademicView(filter, AcademicReportSection.Combined, nameof(Academic), ct);

    [HttpGet, Authorize(Roles = "Admin")]
    public Task<IActionResult> StudentPerformance([FromQuery] AcademicReportFilter filter, CancellationToken ct) =>
        BuildAcademicView(filter, AcademicReportSection.StudentPerformance, nameof(StudentPerformance), ct);

    [HttpGet, Authorize(Roles = "Admin")]
    public Task<IActionResult> AssignmentReport([FromQuery] AcademicReportFilter filter, CancellationToken ct) =>
        BuildAcademicView(filter, AcademicReportSection.AssignmentReport, nameof(AssignmentReport), ct);

    private async Task<IActionResult> BuildAcademicView(AcademicReportFilter filter, AcademicReportSection section, string action, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Choose valid year, subject and class filters.");
        ViewData["ReportSection"] = section;
        ViewData["ReportAction"] = action;
        return View(nameof(Academic), await new AcademicReportService(db).BuildAsync(User, filter, ct));
    }

    [HttpGet]
    public async Task<IActionResult> AcademicExport([FromQuery] AcademicReportFilter filter, string format, CancellationToken ct, AcademicReportSection section = AcademicReportSection.Combined)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(section) || format is not ("pdf" or "excel"))
            return BadRequest("Choose a valid report format and filters.");
        if (section != AcademicReportSection.Combined && !User.IsInRole("Admin")) return Forbid();
        var report = await new AcademicReportService(db).BuildAsync(User, filter, ct);
        var title = AcademicReportExport.Title(section);
        var bytes = format == "pdf" ? AcademicReportExport.Pdf(report, section) : AcademicReportExport.Excel(report, section);
        await securityEvents.RecordAsync(HttpContext, "ReportExported", SecuritySeverity.Information,
            $"Generated {title} report ({format.ToUpperInvariant()}).");
        return File(bytes,
            format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"EduSmart - {title} - {filter.Year}." + (format == "pdf" ? "pdf" : "xlsx"));
    }

    private static string ReportFileName(IncomeReport report, string extension)
    {
        var start = report.Months[0].Month.ToString("MM-yyyy", CultureInfo.InvariantCulture);
        var end = report.EndMonth.ToString("MM-yyyy", CultureInfo.InvariantCulture);
        var period = report.Months.Count == 1 ? end : $"{start} to {end}";
        return $"EduSmart - Income - {period}.{extension}";
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Excel(string? end, int? months, CancellationToken ct)
    {
        if (!ModelState.IsValid || (months ?? 1) is < 1 or > 12) return BadRequest("Choose a period from 1 to 12 months.");
        if (!IncomeReportService.TryEndMonth(end, DateTime.UtcNow, out var month)) return BadRequest();
        var report = await new IncomeReportService(db).BuildAsync(month, months ?? 1, ct);
        var bytes = IncomeReportExport.Excel(report);
        await securityEvents.RecordAsync(HttpContext, "ReportExported", SecuritySeverity.Information, "Generated Income report (EXCEL).");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ReportFileName(report, "xlsx"));
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Pdf(string? end, int? months, CancellationToken ct)
    {
        if (!ModelState.IsValid || (months ?? 1) is < 1 or > 12) return BadRequest("Choose a period from 1 to 12 months.");
        if (!IncomeReportService.TryEndMonth(end, DateTime.UtcNow, out var month)) return BadRequest();
        var report = await new IncomeReportService(db).BuildAsync(month, months ?? 1, ct);
        var bytes = IncomeReportExport.Pdf(report);
        await securityEvents.RecordAsync(HttpContext, "ReportExported", SecuritySeverity.Information, "Generated Income report (PDF).");
        return File(bytes, "application/pdf", ReportFileName(report, "pdf"));
    }
}
