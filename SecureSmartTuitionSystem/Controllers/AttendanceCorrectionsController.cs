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
    public async Task<IActionResult> Index(string? status = null, string sort = "requested_asc", string? search = null, DateTime? from = null, DateTime? to = null, bool fragment = false)
    {
        status ??= Role == "Tutor" ? "confirmation" : Role == "Admin" ? "review" : "pending";
        if (!ModelState.IsValid || status is not ("pending" or "approved" or "rejected" or "all" or "confirmation" or "review") || !AttendanceCorrectionService.ValidSort(sort)) return BadRequest();
        if (search?.Length > 100 || (from.HasValue && from.Value.Year is < 2000 or > 2100) || (to.HasValue && to.Value.Year is < 2000 or > 2100) || (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)) return BadRequest("Choose valid search and date filters.");
        var model = await new AttendanceCorrectionService(db).ListAsync(OwnId, Role == "Admin", status, sort, Role == "Tutor", search, from, to);
        return fragment ? PartialView("_CorrectionResults", model) : View(model);
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
        return WorkflowResult(error, "Correction review saved.");
    }
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> RequestTutorConfirmation(int id, string? rowVersion, string? fallbackTutorId)
    {
        var error = ModelState.IsValid ? await new AttendanceCorrectionService(db).RequestTutorConfirmationAsync(id, rowVersion, OwnId, User.Identity?.Name ?? OwnId, fallbackTutorId) : "Check the request values.";
        return WorkflowResult(error, "Tutor confirmation requested. Attendance remains unchanged.", error == null ? "confirmation" : "all");
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Confirm(int id, bool? supports, string? confirmationRemark, string? rowVersion)
    {
        if (Role != "Tutor") return Forbid();
        var error = ModelState.IsValid ? await new AttendanceCorrectionService(db).ConfirmAsync(id, supports, confirmationRemark, rowVersion, OwnId, User.Identity?.Name ?? OwnId) : "Check the confirmation values.";
        return WorkflowResult(error, "Confirmation sent to Admin. Attendance remains unchanged.", "confirmation");
    }
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReassignConfirmation(int id, string? tutorId, string? rowVersion)
    {
        var error = ModelState.IsValid ? await new AttendanceCorrectionService(db).ReassignConfirmationAsync(id, tutorId, rowVersion, OwnId) : "Check the assignment values.";
        return WorkflowResult(error, "Confirmation assigned to the selected tutor.", "confirmation");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return BadRequest();
        var model = await new AttendanceCorrectionService(db).ListAsync(OwnId, Role == "Admin", "all", tutor: Role == "Tutor", requestId: id);
        var row = model.Rows.SingleOrDefault();
        if (row == null) return NotFound();
        ViewData["CorrectionTutorChoices"] = model.TutorChoices;
        return PartialView("_ConfirmationRequest", row);
    }
    private IActionResult WorkflowResult(string? error, string success, string? status = null)
    {
        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return error == null ? Json(new { ok = true, message = success }) : BadRequest(new { ok = false, message = error });
        TempData[error == null ? "AttendanceCorrectionSuccess" : "AttendanceCorrectionError"] = error ?? success;
        return RedirectToAction(nameof(Index), new { status });
    }

}
