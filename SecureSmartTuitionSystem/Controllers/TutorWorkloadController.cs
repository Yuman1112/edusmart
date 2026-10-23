using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Services.Tutor;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TutorWorkloadController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? week, string? userId)
    {
        if (!ModelState.IsValid || userId?.Length > 450) return BadRequest();
        var date = week?.Date ?? ClassScheduleService.Today;
        if (date.Year < 2000 || date.Year > 2100) return BadRequest();
        var model = await new TutorWorkloadService(db).GetAsync(date, userId);
        return model == null ? NotFound() : View(model);
    }
}