using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Student,Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LearningClassesController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? studentId = null, DateTime? date = null, string mode = "month")
    {
        var anchor = date?.Date ?? ClassScheduleService.Today;
        if (anchor.Year < 2000 || anchor.Year > 2100) return BadRequest();
        var model = await BuildModelAsync(studentId, anchor, mode);
        return model == null ? NotFound() : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, DateTime date, int? studentId = null, int? changeId = null, bool replacement = false)
    {
        if (date.Year < 2000 || date.Year > 2100) return BadRequest();
        var model = await BuildModelAsync(studentId, date.Date, "today");
        if (model == null) return NotFound();
        var lesson = model.Lessons.FirstOrDefault(x => x.Class.ClassSessionId == id && x.ChangeId == changeId && x.Replacement == replacement);
        if (lesson == null) return NotFound();
        model.Lessons = new List<LessonItem> { lesson };
        return View(model);
    }
    private Task<LearningClassesModel?> BuildModelAsync(int? studentId, DateTime anchor, string mode) =>
        new LearningScheduleReader(db).BuildAsync(User, studentId, anchor, mode);
}
