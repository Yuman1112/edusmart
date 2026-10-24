using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Attendance;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Admin,Tutor,Student")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttendanceCorrectionsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private string OwnId => users.GetUserId(User)!;
    private string Role => User.IsInRole("Admin") ? "Admin" : User.IsInRole("Tutor") ? "Tutor" : "Student";
    [HttpGet]
    public async Task<IActionResult> Index(string status = "pending")
    {
        if (!ModelState.IsValid || status is not ("pending" or "approved" or "rejected" or "all")) return BadRequest();
        return View(await new AttendanceCorrectionService(db).ListAsync(OwnId, Role == "Admin", status));
    }
    [HttpGet]
    [Authorize(Roles = "Tutor,Student")]
    public async Task<IActionResult> Create(int recordId)
    {
        if (Role == "Admin") return Forbid();
        var model = await new AttendanceCorrectionService(db).CreateModelAsync(recordId, OwnId, Role);
        return model == null ? NotFound() : View(model);
    }
    [HttpPost]
    [Authorize(Roles = "Tutor,Student")]
    public async Task<IActionResult> Create(int recordId, AttendanceStatus? requestedStatus, string? reason)
    {
        if (Role == "Admin") return Forbid();
        var service = new AttendanceCorrectionService(db);
        var model = await service.CreateModelAsync(recordId, OwnId, Role);
        if (model == null) return NotFound();
        model.RequestedStatus = requestedStatus;
        model.Reason = reason ?? "";

        var invalidStatusBinding =
            ModelState.TryGetValue(nameof(requestedStatus), out var statusEntry)
            && statusEntry.Errors.Count > 0;

        ModelState.Clear();

        if (invalidStatusBinding
            || !requestedStatus.HasValue
            || !Enum.IsDefined(requestedStatus.Value))
        {
            ModelState.AddModelError(
                nameof(model.RequestedStatus),
                "Please select a valid attendance status.");
        }
        else if (requestedStatus == model.CurrentStatus)
        {
            ModelState.AddModelError(
                nameof(model.RequestedStatus),
                "Please select a status different from the current attendance.");
        }

        var trimmedReason = reason?.Trim();

        if (string.IsNullOrEmpty(trimmedReason))
        {
            ModelState.AddModelError(
                nameof(model.Reason),
                "Please enter a correction reason.");
        }
        else if (trimmedReason.Length > 500)
        {
            ModelState.AddModelError(
                nameof(model.Reason),
                "Please enter a correction reason of no more than 500 characters.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Service still validates permissions, duplicate requests and database changes.
        var error = await service.RequestAsync(
            recordId,
            requestedStatus,
            reason,
            OwnId,
            Role,
            User.Identity?.Name ?? OwnId);

        if (error != null)
        {
            var field = error switch
            {
                "Choose a status different from the current attendance."
                    => nameof(model.RequestedStatus),

                "Select a valid attendance status."
                    => nameof(model.RequestedStatus),

                "Enter a correction reason of 1–500 characters."
                    => nameof(model.Reason),

                _ => ""
            };

            ModelState.AddModelError(field, error);
            return View(model);
        }
        TempData["AttendanceCorrectionSuccess"] = "Correction request submitted. Attendance stays unchanged until an administrator approves it.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(int id, string decision, string? reviewRemark, string? rowVersion)
    {
        var error = ModelState.IsValid ? await new AttendanceCorrectionService(db).ReviewAsync(id, decision, reviewRemark, rowVersion, OwnId, User.Identity?.Name ?? OwnId) : "Check the review values.";
        TempData[error == null ? "AttendanceCorrectionSuccess" : "AttendanceCorrectionError"] = error ?? "Correction review saved.";
        return RedirectToAction(nameof(Index));
    }
}
