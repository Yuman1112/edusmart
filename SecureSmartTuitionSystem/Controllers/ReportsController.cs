using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Reporting;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
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

    private static string ReportFileName(IncomeReport report, string extension)
    {
        var start = report.Months[0].Month.ToString("MM-yyyy", CultureInfo.InvariantCulture);
        var end = report.EndMonth.ToString("MM-yyyy", CultureInfo.InvariantCulture);
        var period = report.Months.Count == 1 ? end : $"{start} to {end}";
        return $"EduSmart - Income - {period}.{extension}";
    }

    [HttpGet]
    public async Task<IActionResult> Excel(string? end, int? months, CancellationToken ct)
    {
        if (!ModelState.IsValid || (months ?? 1) is < 1 or > 12) return BadRequest("Choose a period from 1 to 12 months.");
        if (!IncomeReportService.TryEndMonth(end, DateTime.UtcNow, out var month)) return BadRequest();
        var report = await new IncomeReportService(db).BuildAsync(month, months ?? 1, ct);
        return File(IncomeReportExport.Excel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ReportFileName(report, "xlsx"));
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(string? end, int? months, CancellationToken ct)
    {
        if (!ModelState.IsValid || (months ?? 1) is < 1 or > 12) return BadRequest("Choose a period from 1 to 12 months.");
        if (!IncomeReportService.TryEndMonth(end, DateTime.UtcNow, out var month)) return BadRequest();
        var report = await new IncomeReportService(db).BuildAsync(month, months ?? 1, ct);
        return File(IncomeReportExport.Pdf(report), "application/pdf", ReportFileName(report, "pdf"));
    }
}
