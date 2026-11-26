using SecureSmartTuitionSystem.Services.Reporting;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Progress;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Progress;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles="Admin,Tutor")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ClassProgressController(ApplicationDbContext db, SecurityEventService securityEvents):Controller
{
    private ClassProgressService Service=>new(db);
    [HttpGet] public async Task<IActionResult> Index([FromQuery]ProgressFilter filter)
    { if(!ModelState.IsValid)return BadRequest("Choose a valid year, month, subject and class.");return View(await Service.IndexAsync(User,filter)); }
    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] ProgressFilter filter, string format)
    {
        if (!ModelState.IsValid || format is not ("pdf" or "excel"))
            return BadRequest("Choose a valid reporting period and file format.");

        var report = await Service.IndexAsync(User, filter);
        const string title = "Class Progress";
        var period = ReportDownloadNames.Period(filter.Start, filter.End);
        List<string[]> rows =
        [
            [title, period],
            ["Class name", "Schedule", "Subject", "Level", "Tutor", "Ended lessons", "Recorded", "Missing"]
        ];
        rows.AddRange(report.Rows.Select(row => new[] { row.Name, row.Schedule, row.Subject, row.Level,
            row.Tutor, row.Expected.ToString(), row.Submitted.ToString(), row.Missing.ToString() }));
        bool pdf = format == "pdf";
        var bytes = pdf ? AcademicReportExport.Pdf(rows, $"EduSmart - {title} - {period}") : AcademicReportExport.Excel(rows);
        await securityEvents.RecordAsync(HttpContext, "ReportExported", SecuritySeverity.Information,
            $"Generated {title} report ({format.ToUpperInvariant()}).");
        return File(bytes, pdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportDownloadNames.FileName(title, filter.Start, filter.End, pdf ? "pdf" : "xlsx"));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, int? month = null, DateTime? date = null, int key = 0,
        int historyPage = 1, int? endMonth = null, [FromQuery(Name = "overview")] ProgressFilter? overview = null)
    {
        if (!ModelState.IsValid || id < 1 || month is < 1 or > 12 || endMonth is < 1 or > 12 ||
            key < 0 || historyPage is < 1 or > 10000 || date?.Year is < 2000 or > 2100)
            return BadRequest("Choose a valid lesson.");

        var start = month ?? date?.Month ?? ClassScheduleService.Today.Month;
        var end = endMonth ?? start;
        if (end < start || (date.HasValue && (date.Value.Month < start || date.Value.Month > end)))
            return BadRequest("Choose a lesson in the selected period.");

        var model = await Service.DetailsAsync(User, id, start, date, key, historyPage, end);
        if (model is null) return NotFound();
        model.OverviewFilter = Request.Query.ContainsKey("overview.Year") ? overview : null;
        return View(model);
    }
    [HttpGet,Authorize(Roles="Admin")] public async Task<IActionResult> Syllabus(int id)
    {if(!ModelState.IsValid||id<1)return BadRequest();var m=await Service.SyllabusAsync(User,id);return m==null?NotFound():View(m);}
    [HttpPost,Authorize(Roles="Admin"),RequestSizeLimit(262144)] public async Task<IActionResult> Syllabus(SyllabusForm form)
    {
        if(!ModelState.IsValid)return BadRequest("Check chapter names, tracking date and outline version.");
        var error=await Service.SaveSyllabusAsync(User,form);TempData[error==null?"ProgressSuccess":"ProgressError"]=error??"Subject outline saved.";
        return RedirectToAction(nameof(Syllabus),new{id=form.ClassId});
    }
    [HttpPost,Authorize(Roles="Tutor"),RequestSizeLimit(262144)] public async Task<IActionResult> Save(LessonProgressForm form, int? month, int? endMonth, [FromQuery(Name = "overview")] ProgressFilter? overview)
    {
        if(User.IsInRole("Admin"))return Forbid();
        if(!ModelState.IsValid)return BadRequest("Check the lesson, summary and progress values.");
        if(!await Service.Classes(User).AnyAsync(c=>c.ClassSessionId==form.ClassId))return NotFound();
        var error=await Service.SaveLessonAsync(User,form);TempData[error==null?"ProgressSuccess":"ProgressError"]=error??"Teaching progress saved. Completed subchapters are locked.";
        return ReturnToDetails(form.ClassId, month, endMonth, overview, form.Date, form.ChangeKey);
    }
    [HttpPost,Authorize(Roles="Admin")] public async Task<IActionResult> Reopen(ReopenProgressForm form, int? month, int? endMonth, [FromQuery(Name = "overview")] ProgressFilter? overview)
    {
        if(!ModelState.IsValid)return BadRequest("Enter the correction reason and reload the latest progress.");
        var error=await Service.ReopenAsync(User,form);TempData[error==null?"ProgressSuccess":"ProgressError"]=error??"Progress corrected. The original values and reason are retained in history.";
        return ReturnToDetails(form.ClassId, month, endMonth, overview);
    }

    private RedirectToActionResult ReturnToDetails(int classId, int? month, int? endMonth,
        ProgressFilter? overview, DateTime? date = null, int? key = null)
    {
        var route = new Microsoft.AspNetCore.Routing.RouteValueDictionary();
        if (Request.Query.ContainsKey("overview.Year") && overview is not null)
            foreach (var item in overview.RouteValues("overview.")) route[item.Key] = item.Value;

        route["id"] = classId;
        route["month"] = month;
        route["endMonth"] = endMonth;
        route["date"] = date?.ToString("yyyy-MM-dd");
        route["key"] = key;
        return RedirectToAction(nameof(Details), route);
    }
}
