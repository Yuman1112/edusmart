using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminTutorAvailabilityController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string userId, bool partial = false, DateTime? week = null)
    {
        if (!ModelState.IsValid || (week.HasValue && (week.Value.Year < 2000 || week.Value.Year > 2100))) return BadRequest();
        if (string.IsNullOrWhiteSpace(userId)) return NotFound();
        var model = await db.TutorProfiles.AsNoTracking()
            .Where(p => p.ApplicationUserId == userId)
            .Select(p => new TutorAvailabilityOverviewViewModel
            {
                UserId = p.ApplicationUserId,
                TutorId = p.ApplicationUser.UserName ?? "",
                FullName = p.FullName,
                EmploymentType = p.EmploymentType
            }).FirstOrDefaultAsync();
        if (model == null) return NotFound();
        var availability = new TutorAvailabilityService(db);
        var raw = await availability.GetStoredSlotsAsync(userId);
        var normal = await availability.GetSlotsAsync(userId);
        model.WeekStart=TutorAvailabilityPolicy.WeekStart(week ?? SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService.Today);
        model.SelectedSlots=await new TutorDatedAvailabilityService(db).WeekAsync(userId,model.WeekStart);
        model.AdjustedDays=Enumerable.Range(1,6).Where(day=>!normal.Where(x=>x/48==day).ToHashSet().SetEquals(model.SelectedSlots.Where(x=>x/48==day))).ToList();
        model.LegacySlots = model.EmploymentType == SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.FullTime ? [] : raw.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList();
        var lessons=await new SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService(db).GetLessonsAsync(model.WeekStart,model.WeekStart.AddDays(6),userId);
        model.OutsideLessonCount=lessons.Count(x=>!x.Cancelled&&!TutorAvailabilityService.Covers(model.SelectedSlots,(int)x.Date.DayOfWeek,x.Start,x.End));
        return partial ? PartialView("_AvailabilityState", model) : View(model);
    }
}