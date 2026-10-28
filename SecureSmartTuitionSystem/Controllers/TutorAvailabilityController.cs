using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.ViewModels.Tutor;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Tutor")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TutorAvailabilityController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private async Task<TutorProfile?> OwnAsync()
    {
        var id = users.GetUserId(User);
        return await db.TutorProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.ApplicationUserId == id
            && p.IsActive && p.ApplicationUser.IsActive && !p.ApplicationUser.MustChangePassword);
    }
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tutor = await OwnAsync();
        if (tutor == null || !TutorAvailabilityPolicy.IsTypeValid(tutor.EmploymentType)) return Forbid();
        var availability = new TutorAvailabilityService(db);
        var raw = await availability.GetStoredSlotsAsync(tutor.ApplicationUserId);
        return View(new TutorAvailabilityViewModel
        {
            EmploymentType = tutor.EmploymentType,
            SelectedSlots = await availability.GetSlotsAsync(tutor.ApplicationUserId),
            LegacySlots = raw.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList()
        });
    }
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var tutor = await OwnAsync();
        if (tutor == null || tutor.EmploymentType != TutorEmploymentType.PartTime) return Forbid();
        var availability = new TutorAvailabilityService(db);
        var raw = await availability.GetStoredSlotsAsync(tutor.ApplicationUserId);
        return View(new TutorAvailabilityViewModel
        {
            EmploymentType = tutor.EmploymentType,
            SelectedSlots = await availability.GetSlotsAsync(tutor.ApplicationUserId),
            LegacySlots = raw.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList()
        });
    }
    [HttpPost]
    public async Task<IActionResult> Index(TutorAvailabilityViewModel model)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tutor = await OwnAsync();
        if (tutor == null || tutor.EmploymentType != TutorEmploymentType.PartTime) return Forbid();
        model.EmploymentType = tutor.EmploymentType;
        model.SelectedSlots ??= new();
        var existing = await db.TutorAvailabilitySlots.Where(x => x.ApplicationUserId == tutor.ApplicationUserId).ToListAsync();
        var original = existing.Select(x => x.Day * 48 + x.Slot).ToHashSet();
        model.LegacySlots = original.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList();
        if (model.SelectedSlots.Count > 168 || model.SelectedSlots.Any(x => !TutorAvailabilityPolicy.IsOpen(x))
            || model.SelectedSlots.Distinct().Count() != model.SelectedSlots.Count)
            ModelState.AddModelError("", "Please select valid half-hour slots within Monday–Saturday, 08:00–22:00 MYT.");
        if (model.LegacySlots.Count > 0 && !model.RemoveLegacySlots)
            ModelState.AddModelError(nameof(model.RemoveLegacySlots), "Please acknowledge removal of the listed legacy slots before saving.");
        if (!ModelState.IsValid) return View("Edit", model);
        var selected = model.SelectedSlots.ToHashSet();
        if (original.SetEquals(selected))
        {
            TempData["TutorAvailabilityInfo"] = "No changes were made.";
            return RedirectToAction(nameof(Index));
        }
        var warnings = await new TutorAvailabilityService(db).FindExistingClassesOutsideAsync(tutor.ApplicationUserId, selected);
        db.TutorAvailabilitySlots.RemoveRange(existing.Where(x => !selected.Contains(x.Day * 48 + x.Slot)));
        foreach (var value in selected.Except(original))
            db.TutorAvailabilitySlots.Add(new TutorAvailabilitySlot
            {
                ApplicationUserId = tutor.ApplicationUserId,
                Day = value / 48,
                Slot = value % 48
            });
        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is System.Data.Common.DbException)
        {
            await tx.RollbackAsync(); db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Availability could not be saved. Reload and try again; your changes have not been confirmed.");
            return View("Edit", model);
        }
        if (warnings.Count > 0)
            TempData["TutorAvailabilityWarning"] = "Availability saved. Existing lessons remain scheduled outside your new availability: "
                + string.Join("; ", warnings.Take(5)) + (warnings.Count > 5 ? "; and more." : ".")
                + " Contact your administrator to review these lessons.";
        TempData["TutorAvailabilitySuccess"] = selected.Count == 0
            ? "Availability saved. You currently have no available teaching slots."
            : "Your weekly teaching availability was saved successfully.";
        return RedirectToAction(nameof(Index));
    }
}
