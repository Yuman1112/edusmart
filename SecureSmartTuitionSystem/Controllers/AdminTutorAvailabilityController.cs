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
                FullName = p.FullName
            }).FirstOrDefaultAsync();
        if (model == null) return NotFound();
        model.SelectedSlots = await new TutorAvailabilityService(db).GetSlotsAsync(userId);
        return View(model);
    }
}