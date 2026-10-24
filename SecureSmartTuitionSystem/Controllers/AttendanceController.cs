using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Attendance;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Attendance;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Admin,Tutor,Student,Parent")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttendanceController(ApplicationDbContext db, UserManager<ApplicationUser> users, AuditLogIntegrity auditIntegrity) : Controller
{
    private string OwnId => users.GetUserId(User)!;
    private bool Admin => User.IsInRole("Admin");
    private string Role => Admin ? "Admin" : User.IsInRole("Tutor") ? "Tutor" : User.IsInRole("Student") ? "Student" : "Parent";
    private static bool ValidDate(DateTime date) => date.Year is >= 2000 and <= 2100;
    [HttpGet]
    [Authorize(Roles = "Admin,Tutor")]
    public async Task<IActionResult> Index(DateTime? week)
    {
        var date = week?.Date ?? ClassScheduleService.Today;
        if (!ModelState.IsValid || !ValidDate(date)) return BadRequest();
        return View(await new AttendanceService(db).IndexAsync(date, OwnId, Admin));
    }
    [HttpGet]
    [Authorize(Roles = "Admin,Tutor")]
    public async Task<IActionResult> Sheet(int classId, DateTime date, int? changeId, bool replacement = false, int? savedId = null)
    {
        if (!ModelState.IsValid || (!savedId.HasValue && (!ValidDate(date) || classId <= 0))) return BadRequest();
        var model = await new AttendanceService(db).SheetAsync(classId, date, changeId, replacement, savedId, OwnId, Admin);
        return model == null ? NotFound() : View(model);
    }
    [HttpPost]
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Save(SaveAttendanceViewModel model)
    {
        if (Admin || !User.IsInRole("Tutor")) return Forbid();
        if (!ValidDate(model.Date) || model.ClassSessionId <= 0) return BadRequest();
        var service = new AttendanceService(db);
        string? error = ModelState.IsValid ? await service.SaveAsync(model, OwnId) : "Please check the attendance values and try again.";
        if (error != null)
        {
            var sheet = await service.SheetAsync(model.ClassSessionId, model.Date, model.ChangeId, model.Replacement, null, OwnId, false);
            if (sheet == null) return NotFound();
            // Preserve the old version so a stale tab cannot overwrite newer records.
            sheet.RowVersion = model.RowVersion ?? "";
            foreach (var row in sheet.Students)
            {
                var posted = model.Students?.FirstOrDefault(x => x.StudentProfileId == row.StudentProfileId);
                if (posted != null) { row.Status = posted.Status; row.Remark = posted.Remark; }
            }
            ModelState.Clear(); ModelState.AddModelError("", error);
            return View("Sheet", sheet);
        }
        TempData["AttendanceSuccess"] = model.FinalSubmit ? "Attendance submitted and locked." : "Attendance draft saved.";
        return RedirectToAction(nameof(Sheet), new { classId = model.ClassSessionId, date = model.Date.ToString("yyyy-MM-dd"), changeId = model.ChangeId, replacement = model.Replacement });
    }

    [HttpGet]
    public async Task<IActionResult> Report(AttendanceReportFilter filter, bool fragment = false)
    {
        if (!ModelState.IsValid || !AttendanceReportService.Valid(filter)) return BadRequest("Choose a valid date range of at most 367 calendar days and valid filters.");
        var model = await new AttendanceReportService(db).GetAsync(filter, OwnId, Role);
        return fragment ? PartialView("_ReportResults", model) : View(model);
    }
    [HttpGet]
    [Authorize(Roles = "Admin,Tutor")]
    public async Task<IActionResult> Print(AttendanceReportFilter filter)
    {
        if (!ModelState.IsValid || !AttendanceReportService.Valid(filter)) return BadRequest();
        var model = await new AttendanceReportService(db).GetAsync(filter, OwnId, Role, true);
        if (model.Total == 0 || model.ExportLimitExceeded) return BadRequest("Choose filters matching 1–5,000 records before printing.");
        return View(model);
    }
    [HttpPost]
    [Authorize(Roles = "Admin,Tutor")]
    public async Task<IActionResult> Download(AttendanceReportFilter filter)
    {
        if (!ModelState.IsValid || !AttendanceReportService.Valid(filter)) return BadRequest();
        var model = await new AttendanceReportService(db).GetAsync(filter, OwnId, Role, true);
        if (model.Total == 0 || model.ExportLimitExceeded) return BadRequest("Choose filters matching 1–5,000 records before exporting.");
        var bytes = AttendanceExportService.Excel(model.Rows);
        var entry = new AuditLogEntry
        {
            ActorUserId = OwnId,
            ActorName = User.Identity?.Name ?? OwnId,
            EventName = "AttendanceRecordsExported",
            Severity = SecuritySeverity.Information,
            EntityName = "AttendanceRecord",
            EntityKey = "Filtered report",
            RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            TraceId = HttpContext.TraceIdentifier,
            Summary = $"Exported {model.Total} attendance records as XLSX, {filter.From:yyyy-MM-dd} to {filter.To:yyyy-MM-dd}."
        };
        auditIntegrity.Seal(entry); db.AuditLogEntries.Add(entry); await db.SaveChangesAsync();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Attendance-{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.xlsx");
    }
}