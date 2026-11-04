using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.ViewModels.Learning;
namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor,Student,Parent")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssessmentsController(ApplicationDbContext db) : Controller
{
    private AssessmentService Service => new(db);
    private void PreviewPolicy()
    {
        var policy = Response.Headers["Content-Security-Policy"].ToString();
        if (policy.Length == 0) policy = "default-src 'self'; img-src 'self' data:;";
        var parts = policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => !x.StartsWith("frame-src ", StringComparison.OrdinalIgnoreCase)).ToList();
        parts.Add("frame-src 'self' blob:"); Response.Headers["Content-Security-Policy"] = string.Join("; ", parts) + ";";
    }
    [HttpGet]
    public async Task<IActionResult> Index(AssessmentFilter filter, bool fragment = false)
    {
        if (User.IsInRole("Parent") && !AssessmentService.Staff(User)) return RedirectToAction(nameof(Progress));
        if (!ModelState.IsValid) return BadRequest("Choose valid assessment filters.");
        var model = await Service.IndexAsync(User, filter); return fragment ? PartialView("_Results", model) : View(model);
    }
    [HttpGet, Authorize(Roles = "Admin,Tutor,Student")]
    public async Task<IActionResult> Details(int id, bool fragment = false)
    {
        var model = await Service.DetailsAsync(User, id); if (model == null) return NotFound();
        if (fragment) return model.IsStaff ? PartialView("_Submissions", model) : Forbid();
        PreviewPolicy(); return View(model);
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Create()
    {
        if (!AssessmentService.Tutor(User)) return Forbid(); return View("Form", await Service.FormAsync(User, 0));
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Edit(int id)
    {
        if (!AssessmentService.Tutor(User)) return Forbid(); var model = await Service.FormAsync(User, id); return model == null ? NotFound() : View("Form", model);
    }
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(33554432), RequestFormLimits(MultipartBodyLengthLimit = 33554432)]
    public async Task<IActionResult> Save(AssessmentForm form, string command = "draft")
    {
        if (!AssessmentService.Tutor(User)) return Forbid();
        if (command is not ("draft" or "publish")) return BadRequest();
        if (form.Id != 0 && await Service.FormAsync(User, form.Id) == null) return NotFound();
        var files = new List<MaterialFile>();
        if (ModelState.IsValid) { var error = await ReadUploads(form.Files, files); if (error != null) ModelState.AddModelError(nameof(form.Files), error); }
        if (ModelState.IsValid)
        {
            var error = await Service.SaveAsync(User, form, files); if (error != null) ModelState.AddModelError("", error);
            else
            {
                if (command == "publish")
                {
                    var updated = await Service.DetailsAsync(User, form.Id);
                    var publishError = updated == null ? "Draft saved, but publication could not be confirmed." : await Service.ActionAsync(User, form.Id, updated.Assessment.RowVersion, "publish");
                    TempData[publishError == null ? "AssessmentSuccess" : "AssessmentError"] = publishError ?? "Assessment published. The eligible student roster is now fixed.";
                }
                else TempData["AssessmentSuccess"] = "Assessment saved.";
                return RedirectToAction(nameof(Details), new { id = form.Id });
            }
        }
        await Service.FillFormAsync(User, form); return View("Form", form);
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> ChangeState(int id, string? rowVersion, string command, bool json = false)
    {
        if (!AssessmentService.Tutor(User)) return Forbid();
        var previous = ModelState.IsValid ? await Service.DetailsAsync(User, id) : null;
        var error = ModelState.IsValid ? await Service.ActionAsync(User, id, rowVersion, command) : "Check the request and reload.";
        bool draft = previous != null && !previous.Assessment.PublishedAtUtc.HasValue;
        var message = error ?? (command == "release" ? "Results released to students and their linked parents."
            : command == "deactivate" ? (draft ? "Draft archived. It remains hidden from students." : "Assessment set to inactive.")
            : command == "reactivate" ? (draft ? "Draft restored. Publish it when ready; students cannot see it yet." : "Assessment reactivated.")
            : "Assessment published.");
        if (json) return StatusCode(error == null ? 200 : 409, new { ok = error == null, message });
        TempData[error == null ? "AssessmentSuccess" : "AssessmentError"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost, Authorize(Roles = "Student"), RequestSizeLimit(33554432), RequestFormLimits(MultipartBodyLengthLimit = 33554432)]
    public async Task<IActionResult> Submit([Bind(Prefix = "Submission")] SubmissionForm form)
    {
        if (AssessmentService.Staff(User)) return Forbid();
        var files = new List<MaterialFile>(); string? error = null;
        if (ModelState.IsValid) { error = await ReadUploads(form.Files, files); if (error == null) error = await Service.SubmitAsync(User, form, files); }
        else error = "Check your note and attachments.";
        if (error == null) { TempData["AssessmentSuccess"] = "Assignment submitted. You can withdraw it before the deadline if it has not been graded."; return RedirectToAction(nameof(Details), new { id = form.AssessmentId }); }
        var model = await Service.DetailsAsync(User, form.AssessmentId); if (model == null) return NotFound();
        ModelState.AddModelError("Submission.Files", error); model.Submission.Note = form.Note;
        model.Submission.KeptFileIds = form.KeptFileIds;
        model.Submission.ParticipantRowVersion = form.ParticipantRowVersion;
        // Use the latest version on a validation return; confirmed submissions remain read only.
        ModelState.Remove("Submission.RowVersion"); PreviewPolicy(); return View("Details", model);
    }
    [HttpPost, Authorize(Roles = "Student")]
    public async Task<IActionResult> Unsubmit(int id, string? rowVersion)
    {
        if (AssessmentService.Staff(User)) return Forbid();
        var error = ModelState.IsValid ? await Service.UnsubmitAsync(User, id, rowVersion) : "Invalid request. Reload and try again.";
        TempData[error == null ? "AssessmentSuccess" : "AssessmentError"] = error ?? "Answer withdrawn. Review your files and submit again before the deadline.";
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Grade(int id)
    {
        var model = await GradeModel(id); if (model == null) return NotFound(); PreviewPolicy(); return View(model);
    }
    [HttpPost, ActionName("Grade"), Authorize(Roles = "Tutor")]
    public async Task<IActionResult> SaveGrade([Bind(Prefix = "Form")] AssessmentGradeForm form)
    {
        var model = await GradeModel(form.ParticipantId); if (model == null) return NotFound();
        if (ModelState.IsValid) { var error = await Service.GradeAsync(User, form); if (error == null) { TempData["AssessmentSuccess"] = "Grade saved. Students see it only after results are released."; return RedirectToAction(nameof(Details), new { id = model.Assessment.Id }); } ModelState.AddModelError("", error); }
        model.Form = form; PreviewPolicy(); return View("Grade", model);
    }
    private async Task<AssessmentGradeModel?> GradeModel(int id)
    {
        if (!AssessmentService.Tutor(User)) return null;
        var ids = Service.Visible(User).Select(x => x.AssessmentId);
        var assessmentId = await db.Set<AssessmentParticipant>().AsNoTracking().Where(x => x.AssessmentParticipantId == id && ids.Contains(x.AssessmentId)).Select(x => (int?)x.AssessmentId).SingleOrDefaultAsync();
        if (!assessmentId.HasValue) return null; var details = await Service.DetailsAsync(User, assessmentId.Value);
        var participant = details?.Participants.SingleOrDefault(x => x.Id == id);
        if (details == null || !details.CanGrade || !details.Assessment.IsActive || participant?.SubmittedAtUtc == null) return null;
        return new() { Assessment = details.Assessment, Participant = participant, Form = new() { ParticipantId = id, RowVersion = participant.RowVersion, Marks = participant.Marks, Feedback = participant.Feedback ?? "" } };
    }
    [HttpGet]
    public async Task<IActionResult> Progress(AssessmentFilter filter, bool fragment = false)
    {
        if (!ModelState.IsValid) return BadRequest("Choose valid result filters.");
        var model = await Service.ProgressAsync(User, filter); return fragment ? PartialView("_ProgressResults", model) : View(model);
    }
    [HttpGet, Authorize(Roles = "Admin,Tutor,Student")]
    public async Task<IActionResult> Download(int id, bool preview = false)
    {
        var ids = Service.Visible(User).Select(a => a.AssessmentId);
        var q = db.Set<AssessmentFile>().AsNoTracking().Where(f => f.AssessmentFileId == id && ids.Contains(f.AssessmentId));
        if (!AssessmentService.Staff(User))
        {
            var owners = Service.ResultScope(User).Select(p => p.AssessmentParticipantId);
            q = q.Where(f => f.AssessmentParticipantId == null || owners.Contains(f.AssessmentParticipantId.Value));
        }
        var file = await q.Select(f => new { f.Name, f.ContentType, f.Content }).SingleOrDefaultAsync(); if (file == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var type = MaterialFileService.PreviewContentType(file.Name);
        if (preview) { if (type == null) return BadRequest("Preview is available for PDF and images only."); Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self' data:; frame-ancestors 'self'; base-uri 'none';"; Response.Headers["X-Frame-Options"] = "SAMEORIGIN"; return File(file.Content, type, enableRangeProcessing: true); }
        return File(file.Content, file.ContentType, MaterialFileService.SafeName(file.Name));
    }
    private async Task<string?> ReadUploads(List<IFormFile> uploads, List<MaterialFile> output)
    {
        if (uploads.Count > 10 || uploads.Sum(f => f.Length) > 31457280) return "Choose up to 10 files and 30 MB total.";
        foreach (var upload in uploads) { var result = await MaterialFileService.ReadAsync(upload, HttpContext.RequestAborted); if (result.Error != null) return result.Error; output.Add(result.File!); }
        return null;
    }
}
