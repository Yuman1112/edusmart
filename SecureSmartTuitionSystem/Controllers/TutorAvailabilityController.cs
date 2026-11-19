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
    public async Task<IActionResult> Index(bool partial = false)
    {
        var tutor = await OwnAsync();
        if (tutor == null || !TutorAvailabilityPolicy.IsTypeValid(tutor.EmploymentType)) return Forbid();
        var availability = new TutorAvailabilityService(db);
        var raw = await availability.GetStoredSlotsAsync(tutor.ApplicationUserId);
        var model = new TutorAvailabilityViewModel
        {
            EmploymentType = tutor.EmploymentType,
            Version = TutorAvailabilityPolicy.Version(tutor.EmploymentType, raw),
            SelectedSlots = raw.Where(TutorAvailabilityPolicy.IsOpen).ToList(),
            LegacySlots = raw.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList()
        };
        return partial ? PartialView("_AvailabilityState", model) : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var tutor = await OwnAsync();
        if (tutor == null || !TutorAvailabilityPolicy.IsTypeValid(tutor.EmploymentType)) return Forbid();
        var availability = new TutorAvailabilityService(db);
        var raw = await availability.GetStoredSlotsAsync(tutor.ApplicationUserId);
        return View(new TutorAvailabilityViewModel
        {
            EmploymentType = tutor.EmploymentType,
            Version = TutorAvailabilityPolicy.Version(tutor.EmploymentType, raw),
            SelectedSlots = raw.Where(TutorAvailabilityPolicy.IsOpen).ToList(),
            LegacySlots = raw.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList()
        });
    }
    [HttpPost]
    public async Task<IActionResult> Index(TutorAvailabilityViewModel model)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tutor = await OwnAsync();
        if (tutor == null || !TutorAvailabilityPolicy.IsTypeValid(tutor.EmploymentType)) return Forbid();
        model.EmploymentType = tutor.EmploymentType;
        model.SelectedSlots ??= new();
        var existing = await db.TutorAvailabilitySlots.Where(x => x.ApplicationUserId == tutor.ApplicationUserId).ToListAsync();
        var original = existing.Select(x => x.Day * 48 + x.Slot).ToHashSet();
        var version = TutorAvailabilityPolicy.Version(tutor.EmploymentType, original);
        if (!string.Equals(model.Version, version, StringComparison.Ordinal))
        {
            const string message = "Your saved availability changed in another page. Reload the latest availability before saving.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Conflict(new { message, stale = true });
            ModelState.AddModelError("", message);
            ViewData["AvailabilityStale"] = true;
            return View("Edit", model);
        }
        model.LegacySlots = original.Where(x => !TutorAvailabilityPolicy.IsOpen(x)).ToList();
        if (model.SelectedSlots.Count > 6 * TutorAvailabilityPolicy.MaxDailySlots || model.SelectedSlots.Any(x => !TutorAvailabilityPolicy.IsOpen(x))
            || model.SelectedSlots.Distinct().Count() != model.SelectedSlots.Count)
            ModelState.AddModelError("", "Please select valid half-hour slots within Monday–Saturday, 08:00–22:00 MYT.");
        var overLimit = TutorAvailabilityPolicy.DaysOverLimit(model.SelectedSlots);
        if (overLimit.Count > 0)
            ModelState.AddModelError("", "Select no more than eight hours per day. Review: "
                + string.Join(", ", overLimit.Select(day => ((DayOfWeek)day).ToString())) + ".");
        if (model.LegacySlots.Count > 0 && !model.RemoveLegacySlots)
            ModelState.AddModelError(nameof(model.RemoveLegacySlots), "Please acknowledge removal of the listed legacy slots before saving.");
        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return BadRequest(new { message = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage)) });
            return View("Edit", model);
        }
        var selected = model.SelectedSlots.ToHashSet();
        if (original.SetEquals(selected))
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, version, message = "No changes were made.", warning = "" });
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
        catch (Exception ex) when (
            ex is DbUpdateException ||
            ex is System.Data.Common.DbException)
        {
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();

            const string message = "Availability could not be saved. Your changes have not been confirmed. Please try saving again.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return StatusCode(503, new { message });
            ModelState.AddModelError("", message);

            ViewData["AvailabilitySaveFailed"] = true;

            return View("Edit", model);
        }
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, version = TutorAvailabilityPolicy.Version(tutor.EmploymentType, selected),
                message = selected.Count == 0 ? "Availability saved. You currently have no available teaching slots." : "Your weekly teaching availability was saved successfully.",
                warning = warnings.Count == 0 ? "" : "Existing lessons remain outside your new availability: " + string.Join("; ", warnings.Take(5)) + ". Contact your administrator to review them." });
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
