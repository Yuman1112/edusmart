using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.ViewModels.Tutor;
using System.Data;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Tutor")]
[AutoValidateAntiforgeryToken]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class TutorAvailabilityController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public TutorAvailabilityController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<string?> GetOwnActiveTutorIdAsync()
    {
        string? userId = _users.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
            return null;

        return await _db.TutorProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.ApplicationUserId == userId
                && profile.IsActive
                && profile.ApplicationUser.IsActive
                && !profile.ApplicationUser.MustChangePassword)
            .Select(profile => profile.ApplicationUserId)
            .FirstOrDefaultAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string? userId = await GetOwnActiveTutorIdAsync();

        if (userId == null)
            return Forbid();

        var model = new TutorAvailabilityViewModel
        {
            SelectedSlots = await _db.TutorAvailabilitySlots
                .AsNoTracking()
                .Where(slot =>
                    slot.ApplicationUserId == userId)
                .OrderBy(slot => slot.Day)
                .ThenBy(slot => slot.Slot)
                .Select(slot => slot.Day * 48 + slot.Slot)
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index(
        TutorAvailabilityViewModel model)
    {
        string? userId = await GetOwnActiveTutorIdAsync();

        if (userId == null)
            return Forbid();

        model.SelectedSlots ??= new List<int>();

        bool invalidSlots =
            model.SelectedSlots.Count > 336
            || model.SelectedSlots.Any(slot =>
                slot < 0 || slot >= 336)
            || model.SelectedSlots.Distinct().Count()
                != model.SelectedSlots.Count;

        if (invalidSlots)
        {
            ModelState.AddModelError(
                string.Empty,
                "Please select valid availability slots and save again.");
        }

        if (!ModelState.IsValid)
            return View(model);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var existing = await _db.TutorAvailabilitySlots
            .Where(slot =>
                slot.ApplicationUserId == userId)
            .ToListAsync();

        var selected = model.SelectedSlots.ToHashSet();

        var original = existing
            .Select(slot => slot.Day * 48 + slot.Slot)
            .ToHashSet();

        if (original.SetEquals(selected))
        {
            TempData["TutorAvailabilityInfo"] =
                "No changes were made.";

            return RedirectToAction(nameof(Index));
        }

        var outsideClasses = await new TutorAvailabilityService(_db)
            .FindExistingClassesOutsideAsync(userId, selected);

        var removedSlots = existing.Where(slot =>
            !selected.Contains(slot.Day * 48 + slot.Slot));

        _db.TutorAvailabilitySlots.RemoveRange(removedSlots);

        foreach (int value in selected.Except(original))
        {
            _db.TutorAvailabilitySlots.Add(
                new TutorAvailabilitySlot
                {
                    ApplicationUserId = userId,
                    Day = value / 48,
                    Slot = value % 48
                });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (outsideClasses.Count > 0)
        {
            TempData["TutorAvailabilityWarning"] =
                "Availability saved. Existing lessons outside your new availability remain scheduled: "
                + string.Join("; ", outsideClasses.Take(5))
                + (outsideClasses.Count > 5 ? $"; and {outsideClasses.Count - 5} more." : ".")
                + " Contact your administrator if these lessons need to change.";
        }

        TempData["TutorAvailabilitySuccess"] =
            selected.Count == 0
                ? "Availability saved. You currently have no available teaching slots."
                : "Your weekly teaching availability was saved successfully.";

        return RedirectToAction(nameof(Index));
    }
}