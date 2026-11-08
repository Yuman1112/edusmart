using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(VideoFileStore.RequestBytes), RequestFormLimits(MultipartBodyLengthLimit = VideoFileStore.RequestBytes)]
    public async Task<IActionResult> Save(VideoForm form, CancellationToken ct)
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        var error = VideoLessonService.Validate(form, Services.Enrollment.ClassScheduleService.Today);
        if (Request.Form.Files.Count > 1) ModelState.AddModelError(nameof(form.VideoFile), "Upload videos using the batch uploader. Reload the page and enable JavaScript, then select your videos again.");
        if (error != null) ModelState.AddModelError(nameof(form.RecordingDate), error);
        if (form.Id == 0 && form.VideoFile == null) ModelState.AddModelError(nameof(form.VideoFile), "Please select an MP4 video.");
        if (form.Id != 0 && form.VideoFile != null) ModelState.AddModelError(nameof(form.VideoFile), "The saved video cannot be replaced. Upload another lesson instead.");
        (string Key, long Size, double Duration)? file = null;
        bool committed = false;
        try
        {
            if (ModelState.IsValid && form.Id == 0)
            {
                if (!await new LearningMaterialService(db).Classes(User, true).AnyAsync(c => c.ClassSessionId == form.ClassSessionId, ct)) ModelState.AddModelError(nameof(form.ClassSessionId), "This assigned class is no longer available.");
                else file = await Store.StoreAsync(form.VideoFile!, ct);
            }
            if (ModelState.IsValid)
            {
                var result = await Service.SaveAsync(User, form, file);
                if (result.Error == null)
                {
                    committed = true;
                    TempData["VideoSuccess"] = form.Id == 0 ? "Recorded lesson uploaded successfully." : "Recorded lesson updated successfully.";
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
            if (file.HasValue && !committed) Store.Remove(file.Value.Key);
        }
        if (Request.Headers.Accept.ToString().Contains("application/json")) return BadRequest(new { errors = ModelState.Where(x => x.Value!.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Check this value." : e.ErrorMessage).ToArray()) });
        AllowLocalPreview(); await Service.PopulateAsync(User, form); return View("Form", form);
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> ChangeState(int id, string? rowVersion, bool active)
    {
        if (!VideoLessonService.IsTutor(User)) return Forbid();
        var error = await Service.StateAsync(User, id, rowVersion, active);
        if (Request.Headers.Accept.ToString().Contains("application/json")) return StatusCode(error == null ? 200 : 409, new { ok = error == null, message = error ?? (active ? "Recorded lesson is active." : "Recorded lesson is inactive. Students can no longer access it.") });
        TempData[error == null ? "VideoSuccess" : "VideoWarning"] = error ?? "Recorded lesson status updated.";
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
    public async Task<IActionResult> Access(int id)
    {
        var exists = await Service.Visible(User).AnyAsync(x => x.RecordedLessonId == id);
        return exists ? Json(new { allowed = true }) : NotFound();
    }
    [HttpGet, HttpHead]
    public async Task<IActionResult> Media(int id)
    {
        var lesson = await Service.Visible(User).Where(x => x.RecordedLessonId == id).Select(x => new { x.StorageKey, x.UpdatedAtUtc }).SingleOrDefaultAsync();
        if (lesson == null) return NotFound();
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
