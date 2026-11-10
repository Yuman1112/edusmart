using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Video;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.ViewModels.Video;
namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor,Student")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VideoLessonsController(ApplicationDbContext db, IWebHostEnvironment env, IConfiguration config, ILogger<VideoLessonsController> logger) : Controller
{
    private VideoLessonService Service => new(db);
    private VideoFileStore Store => new(env, config);
    private void AllowLocalPreview()
    {
        // Keep the application's existing CSP and add only the local video preview source.
        var policy = Response.Headers.ContentSecurityPolicy.ToString();
        if (!policy.Contains("media-src", StringComparison.OrdinalIgnoreCase)) Response.Headers.ContentSecurityPolicy = policy.TrimEnd(';') + "; media-src 'self' blob:";
    }
    [HttpGet]
    public async Task<IActionResult> Index(VideoFilter filter, bool fragment = false)
    {
        if (!ModelState.IsValid) return BadRequest("Choose valid video filters.");
        var model = await Service.IndexAsync(User, filter); return fragment ? PartialView("_Results", model) : View(model);
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Create()
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        AllowLocalPreview();
        return View("Form", await Service.FormAsync(User, 0));
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Edit(int id)
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        AllowLocalPreview();
        var form = await Service.FormAsync(User, id); return form == null ? NotFound() : View("Form", form);
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> ClassChoices()
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        var classes = await new LearningMaterialService(db).Classes(User, true).OrderBy(x => x.ClassCode).Select(x => new { id = x.ClassSessionId, text = x.ClassCode + " · " + x.ClassName, subject = x.Subject.AcademicLevel + " · " + x.Subject.SubjectName }).ToListAsync();
        return Json(new { classes });
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> CheckReplacement(int id, string? rowVersion, long size, string? hash, CancellationToken ct)
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        if (!ModelState.IsValid || id <= 0 || size <= 0 || size > VideoFileStore.MaxBytes
            || hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit))
            return BadRequest();

        var current = await Service.Manage(User).AsNoTracking()
            .Where(x => x.RecordedLessonId == id && !x.InactivatedByAdmin)
            .Select(x => new { x.StorageKey, x.RowVersion })
            .SingleOrDefaultAsync(ct);
        if (current == null)
            return NotFound(new { message = "This recording is no longer available for replacement. Reload the page." });
        if (!AssessmentRules.Version(current.RowVersion, rowVersion))
            return Conflict(new { message = "This recording changed. Reload the page before replacing it." });

        try
        {
            await using var stream = Store.Open(current.StorageKey);
            if (stream == null) return StatusCode(503);
            if (stream.Length != size) return Json(new { duplicate = false });

            var currentHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            return Json(new { duplicate = string.Equals(currentHash, hash, StringComparison.OrdinalIgnoreCase) });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not check the replacement video for recording {RecordingId}.", id);
            return StatusCode(503);
        }
    }
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(VideoFileStore.RequestBytes), RequestFormLimits(MultipartBodyLengthLimit = VideoFileStore.RequestBytes)]
    public async Task<IActionResult> Save(VideoForm form, CancellationToken ct)
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        var error = VideoLessonService.Validate(form, Services.Enrollment.ClassScheduleService.Today);
        if (Request.Form.Files.Count > 1)
            ModelState.AddModelError(nameof(form.VideoFile), form.Id > 0
                ? "Choose one MP4 to replace this recording."
                : "Upload videos using the batch uploader. Reload the page and enable JavaScript, then select your videos again.");
        if (error != null) ModelState.AddModelError(nameof(form.RecordingDate), error);
        if (form.Id == 0 && form.VideoFile == null) ModelState.AddModelError(nameof(form.VideoFile), "Please select an MP4 video.");
        (string Key, long Size, double Duration)? file = null;
        bool discardStagedFile = true;
        try
        {
            if (ModelState.IsValid && form.Id == 0)
            {
                if (!await new LearningMaterialService(db).Classes(User, true).AnyAsync(c => c.ClassSessionId == form.ClassSessionId, ct)) ModelState.AddModelError(nameof(form.ClassSessionId), "This assigned class is no longer available.");
            }
            string? currentVideoKey = null;
            if (ModelState.IsValid && form.Id > 0 && form.VideoFile != null)
            {
                currentVideoKey = await Service.Manage(User)
                    .Where(x => x.RecordedLessonId == form.Id && !x.InactivatedByAdmin)
                    .Select(x => x.StorageKey)
                    .SingleOrDefaultAsync(ct);
                if (currentVideoKey == null)
                    ModelState.AddModelError(nameof(form.VideoFile), "This recording is not available for replacement.");
            }
            if (ModelState.IsValid)
            {
                if (form.VideoFile != null) file = await Store.StoreAsync(form.VideoFile, ct, currentVideoKey);
                // If the commit outcome is unknown, retain the staged file in case the database references it.
                discardStagedFile = false;
                var result = await Service.SaveAsync(User, form, file);
                discardStagedFile = result.Error != null;
                if (result.Error == null)
                {
                    if (result.PreviousKey != null) Store.Remove(result.PreviousKey);
                    TempData["VideoSuccess"] = form.Id == 0 ? "Recorded lesson uploaded successfully."
                        : file.HasValue ? "Video replaced successfully. Student watch progress has been reset." : "Recorded lesson updated successfully.";
                    if (Request.Headers.Accept.ToString().Contains("application/json")) return Json(new { url = Url.Action("Play", new { id = result.Id }) });
                    return RedirectToAction(nameof(Play), new { id = result.Id });
                }
                ModelState.AddModelError("", result.Error);
            }
        }
        catch (InvalidDataException e) { ModelState.AddModelError(nameof(form.VideoFile), e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { logger.LogWarning(e, "Video storage operation failed."); ModelState.AddModelError(nameof(form.VideoFile), "Video storage is unavailable. Please try again or contact your administrator."); }
        finally
        {
            // A committed recording owns its file. Failed database/validation attempts leave no uploaded file.
            if (file.HasValue && discardStagedFile) Store.Remove(file.Value.Key);
        }
        if (Request.Headers.Accept.ToString().Contains("application/json")) return BadRequest(new { errors = ModelState.Where(x => x.Value!.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Check this value." : e.ErrorMessage).ToArray()) });
        if (form.Id > 0)
        {
            var saved = await Service.FormAsync(User, form.Id);
            if (saved == null) return NotFound();
            form.CurrentFileName = saved.CurrentFileName;
            form.CanReplaceVideo = saved.CanReplaceVideo;
        }
        AllowLocalPreview(); await Service.PopulateAsync(User, form); return View("Form", form);
    }
    [HttpPost, Authorize(Roles = "Admin,Tutor")]
    public async Task<IActionResult> ChangeState(int id, string? rowVersion, bool? active)
    {
        if (!ModelState.IsValid || id <= 0 || active is null)
            return BadRequest(new { message = "Choose a valid recording and status." });

        var error = await Service.StateAsync(User, id, rowVersion, active.Value);
        var message = error ?? (active.Value
            ? "Recorded lesson restored."
            : "Recorded lesson set to inactive. Students can no longer access it.");
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return StatusCode(error == null ? 200 : 409, new { ok = error == null, message });

        TempData[error == null ? "VideoSuccess" : "VideoWarning"] = message;
        return RedirectToAction(nameof(Play), new { id });
    }
    [HttpGet]
    public async Task<IActionResult> Play(int id)
    {
        var model = await Service.PlayAsync(User, id);
        if (model != null) return View(model);
        Response.StatusCode = 404;
        return View("Unavailable");
    }
    [HttpGet]
    public async Task<IActionResult> Access(int id, string? version)
    {
        var current = await Service.Visible(User).Where(x => x.RecordedLessonId == id).Select(x => x.StorageKey).SingleOrDefaultAsync();
        if (current == null) return NotFound();
        return version != null && version != current ? StatusCode(409) : Json(new { allowed = true });
    }
    [HttpGet, HttpHead]
    public async Task<IActionResult> Media(int id, string? version)
    {
        var lesson = await Service.Visible(User).Where(x => x.RecordedLessonId == id).Select(x => new { x.StorageKey, x.UpdatedAtUtc }).SingleOrDefaultAsync();
        if (lesson == null || (version != null && version != lesson.StorageKey)) return NotFound();
        try
        {
            var stream = Store.Open(lesson.StorageKey); if (stream == null) return NotFound();
            return new FileStreamResult(stream, "video/mp4") { EnableRangeProcessing = true, LastModified = new DateTimeOffset(DateTime.SpecifyKind(lesson.UpdatedAtUtc, DateTimeKind.Utc)), EntityTag = new EntityTagHeaderValue("\"" + lesson.StorageKey + "\"") };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { logger.LogWarning(e, "Video playback storage unavailable."); return StatusCode(503); }
    }
    [HttpPost, Authorize(Roles = "Student")]
    public async Task<IActionResult> Progress(VideoProgressForm form)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!await Service.ProgressAsync(User, form)) return NotFound();
        var own = LearningMaterialService.UserId(User);
        var watched = await db.VideoWatchProgress.AnyAsync(p => p.RecordedLessonId == form.Id && p.StudentUserId == own && p.CompletedAtUtc != null);
        return Json(new { watched });
    }
}
