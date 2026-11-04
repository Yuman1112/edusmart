using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.ViewModels.Learning;
namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor,Student")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LearningMaterialsController(ApplicationDbContext db) : Controller
{
    private LearningMaterialService Materials => new(db);
    // Scope blob frames/images to the three material pages; keep the rest of the site's CSP unchanged.
    private void AllowMaterialPreviewFrames()
    {
        var policy = Response.Headers["Content-Security-Policy"].ToString();
        if (policy.Length == 0) policy = "default-src 'self'; img-src 'self' data:;";
        var directives = policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        directives.RemoveAll(d => d.StartsWith("frame-src ", StringComparison.OrdinalIgnoreCase));
        var imageIndex = directives.FindIndex(d => d.StartsWith("img-src ", StringComparison.OrdinalIgnoreCase));
        if (imageIndex < 0) directives.Add("img-src 'self' data: blob:");
        else if (!directives[imageIndex].Split(' ').Contains("blob:")) directives[imageIndex] += " blob:";
        directives.Add("frame-src 'self' blob:");
        Response.Headers["Content-Security-Policy"] = string.Join("; ", directives) + ";";
    }

    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> ClassChoices()
    {
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        var classes = await Materials.ChoicesAsync(User, true);
        return Json(new { classes = classes.Select(x => new { id = x.Value, text = x.Text }) });
    }
    [HttpGet]
    public async Task<IActionResult> Index(MaterialFilter filter, bool fragment = false)
    {
        if (!ModelState.IsValid || filter.ClassId is <= 0 || filter.Status is not ("active" or "inactive" or "all")) return BadRequest("Choose valid material filters.");
        var model = await Materials.IndexAsync(User, filter);
        return fragment ? PartialView("_MaterialResults", model) : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        AllowMaterialPreviewFrames();
        var model = await Materials.DetailsAsync(User, id);
        return model == null ? NotFound() : View(model);
    }
    [HttpGet]
    public Task<IActionResult> Download(int id, int? attachmentId = null) => ReadFile(id, attachmentId, false);
    [HttpGet]
    public async Task<IActionResult> DownloadAll(int id)
    {
        // Verify the version again after reading to avoid packaging a partially changed attachment set.
        var material = await Materials.Visible(User).Where(x => x.LearningMaterialId == id)
            .Select(x => new { x.ClassSessionId, x.Title, x.FileSize, x.OriginalFileName, x.RowVersion }).SingleOrDefaultAsync();
        if (material == null) return NotFound();
        if (User.IsInRole("Student") && !User.IsInRole("Admin") && !User.IsInRole("Tutor")
            && !await new LearningAccessService(db).CanAccessAsync(User, material.ClassSessionId)) return NotFound();
        var sizes = await db.Set<LearningMaterialAttachment>().AsNoTracking().Where(a => a.LearningMaterialId == id)
            .Select(a => a.FileSize).Take(10).ToListAsync();
        if (sizes.Count + 1 > 10 || material.FileSize < 1 || material.FileSize > MaterialFileService.MaxBytes
            || sizes.Any(size => size < 1 || size > MaterialFileService.MaxBytes)
            || sizes.Sum(size => (long)size) + material.FileSize > 30L * 1024 * 1024)
            return BadRequest("This material exceeds the attachment download limit. Contact your administrator.");
        var first = await Materials.Visible(User).Where(x => x.LearningMaterialId == id)
            .Select(x => x.FileContent).SingleOrDefaultAsync();
        if (first == null) return NotFound();
        var files = new List<MaterialFile> { new(material.OriginalFileName, "application/octet-stream", first) };
        files.AddRange(await db.Set<LearningMaterialAttachment>().AsNoTracking().Where(a => a.LearningMaterialId == id)
            .OrderBy(a => a.LearningMaterialAttachmentId)
            .Select(a => new MaterialFile(a.OriginalFileName, a.ContentType, a.FileContent)).ToListAsync());
        var current = await Materials.Visible(User).Where(x => x.LearningMaterialId == id)
            .Select(x => x.RowVersion).SingleOrDefaultAsync();
        if (current == null) return NotFound();
        if (!current.AsSpan().SequenceEqual(material.RowVersion))
            return Conflict("This material changed while preparing the download. Reload and try again.");
        byte[] archive;
        try { archive = MaterialFileService.BuildArchive(files); }
        catch (ArgumentException) { return BadRequest("The attachments could not be packaged. Reload the page and try again."); }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        return File(archive, "application/zip", MaterialFileService.SafeName($"Material-{id}-{material.Title}.zip"));
    }
    [HttpGet]
    public Task<IActionResult> Preview(int id, int? attachmentId = null) => ReadFile(id, attachmentId, true);
    private async Task<IActionResult> ReadFile(int id, int? attachmentId, bool preview)
    {
        var material = await Materials.Visible(User).Where(x => x.LearningMaterialId == id)
            .Select(x => new { x.ClassSessionId }).SingleOrDefaultAsync();
        if (material == null) return NotFound();
        if (User.IsInRole("Student") && !User.IsInRole("Admin") && !User.IsInRole("Tutor")
            && !await new LearningAccessService(db).CanAccessAsync(User, material.ClassSessionId)) return NotFound();
        var file = attachmentId.HasValue
            ? await db.Set<LearningMaterialAttachment>().AsNoTracking()
                .Where(a => a.LearningMaterialId == id && a.LearningMaterialAttachmentId == attachmentId)
                .Select(a => new { a.FileContent, a.ContentType, a.OriginalFileName }).SingleOrDefaultAsync()
            : await Materials.Visible(User).Where(a => a.LearningMaterialId == id)
                .Select(a => new { a.FileContent, a.ContentType, a.OriginalFileName }).SingleOrDefaultAsync();
        if (file == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        if (!preview) return File(file.FileContent, file.ContentType, MaterialFileService.SafeName(file.OriginalFileName));
        var type = MaterialFileService.PreviewContentType(file.OriginalFileName);
        if (type == null || !MaterialFileService.Matches(Path.GetExtension(file.OriginalFileName).ToLowerInvariant(), file.FileContent))
            return BadRequest("Preview is available for PDF, PNG and JPG files only. Use Download for other formats.");
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(MaterialFileService.SafeName(file.OriginalFileName));
        Response.Headers["Content-Disposition"] = disposition.ToString();
        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self' data:; frame-ancestors 'self'; base-uri 'none';";
        return File(file.FileContent, type);
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Create()
    {
        AllowMaterialPreviewFrames();
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        return View(new MaterialForm { Classes = await Materials.ChoicesAsync(User, true) });
    }
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(33554432), RequestFormLimits(MultipartBodyLengthLimit = 33554432)]
    public async Task<IActionResult> Create(MaterialForm model, CancellationToken token)
    {
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        model.Id = 0; model.RowVersion = null;
        ModelState.Remove(nameof(model.RowVersion));
        return await SaveForm(model, "Create", token);
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Edit(int id)
    {
        AllowMaterialPreviewFrames();
        var model = await Materials.EditAsync(User, id);
        if (model == null) return NotFound();
        model.Classes = await Materials.ChoicesAsync(User, true);
        return View(model);
    }
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(33554432), RequestFormLimits(MultipartBodyLengthLimit = 33554432)]
    public async Task<IActionResult> Edit(int id, MaterialForm model, CancellationToken token)
    {
        if (id <= 0 || model.Id != id) return BadRequest();
        var existing = await Materials.EditAsync(User, id);
        if (existing == null) return NotFound();
        if (model.ClassSessionId != existing.ClassSessionId) return BadRequest("A material cannot be moved to another class. Upload a separate copy there.");
        model.CurrentFileName = existing.CurrentFileName;
        model.CurrentFiles = existing.CurrentFiles;
        if (string.IsNullOrWhiteSpace(model.RowVersion)) ModelState.AddModelError(nameof(model.RowVersion), "Reload this material before saving.");
        return await SaveForm(model, "Edit", token);
    }
    private async Task<IActionResult> SaveForm(MaterialForm model, string view, CancellationToken token)
    {
        AllowMaterialPreviewFrames();
        if (string.IsNullOrWhiteSpace(model.Title)) ModelState.AddModelError(nameof(model.Title), "Please enter a material title.");
        var uploads = model.Files.ToList();
        if (model.File != null) uploads.Add(model.File);
        var files = new List<MaterialFile>();
        if (uploads.Count > 10 || uploads.Sum(f => f.Length) > 31457280)
            ModelState.AddModelError(nameof(model.Files), "Choose up to 10 files, each at most 10 MB and 30 MB in total.");
        if (model.Id == 0 && uploads.Count == 0)
            ModelState.AddModelError(nameof(model.Files), "Choose at least one material file.");
        if (ModelState.IsValid)
        {
            foreach (var upload in uploads)
            {
                try
                {
                    var result = await MaterialFileService.ReadAsync(upload, token);
                    if (result.Error != null) ModelState.AddModelError(nameof(model.Files), $"{MaterialFileService.SafeName(upload.FileName)}: {result.Error}");
                    else if (result.File != null) files.Add(result.File);
                }
                catch (IOException) { ModelState.AddModelError(nameof(model.Files), "A file could not be read. Re-select the files and try again."); }
            }
        }
        if (ModelState.IsValid)
        {
            var error = await Materials.SaveFilesAsync(User, model, files);
            if (error == null)
            {
                TempData["MaterialSuccess"] = view == "Create" ? "Learning material uploaded successfully." : "Learning material updated successfully.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            ModelState.AddModelError("", error);
        }
        model.Classes = await Materials.ChoicesAsync(User, true);
        return View(view, model);
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> ChangeStatus(int id, string? rowVersion, bool active, bool json = false)
    {
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        var error = ModelState.IsValid ? await Materials.StatusAsync(User, id, rowVersion, active) : "Check the status request and reload.";
        var message = error ?? (active ? "Learning material reactivated." : "Learning material set to inactive. Students can no longer access it.");
        if (json) return StatusCode(error == null ? 200 : 409, new { ok = error == null, message });
        TempData[error == null ? "MaterialSuccess" : "MaterialError"] = message;
        return RedirectToAction(nameof(Index), new { status = "all" });
    }
}
