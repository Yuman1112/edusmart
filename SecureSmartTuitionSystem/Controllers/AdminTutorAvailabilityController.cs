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
    public async Task<IActionResult> Index(string userId)
    {
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
        model.SelectedSlots = await availability.GetSlotsAsync(userId);
        model.LegacySlots = (await availability.GetStoredSlotsAsync(userId)).Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList();
        return View(model);
    }
}