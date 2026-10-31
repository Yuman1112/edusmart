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
        var model = await Materials.DetailsAsync(User, id);
        return model == null ? NotFound() : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var file = await Materials.Visible(User).Where(x => x.LearningMaterialId == id)
            .Select(x => new { x.ClassSessionId, x.FileContent, x.ContentType, x.OriginalFileName }).SingleOrDefaultAsync();
        if (file == null) return NotFound();
        if (User.IsInRole("Student") && !User.IsInRole("Admin") && !User.IsInRole("Tutor")
            && !await new LearningAccessService(db).CanAccessAsync(User, file.ClassSessionId)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.FileContent, file.ContentType, MaterialFileService.SafeName(file.OriginalFileName));
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Create()
    {
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        return View(new MaterialForm { Classes = await Materials.ChoicesAsync(User, true) });
    }
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(12582912), RequestFormLimits(MultipartBodyLengthLimit = 12582912)]
    public async Task<IActionResult> Create(MaterialForm model, CancellationToken token)
    {
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        model.Id = 0; model.RowVersion = "";
        return await SaveForm(model, "Create", token);
    }
    [HttpGet, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await Materials.EditAsync(User, id);
        if (model == null) return NotFound();
        model.Classes = await Materials.ChoicesAsync(User, true);
        return View(model);
    }
    [HttpPost, Authorize(Roles = "Tutor"), RequestSizeLimit(12582912), RequestFormLimits(MultipartBodyLengthLimit = 12582912)]
    public async Task<IActionResult> Edit(int id, MaterialForm model, CancellationToken token)
    {
        if (id <= 0 || model.Id != id) return BadRequest();
        var existing = await Materials.EditAsync(User, id);
        if (existing == null) return NotFound();
        if (model.ClassSessionId != existing.ClassSessionId) return BadRequest("A material cannot be moved to another class. Upload a separate copy there.");
        model.CurrentFileName = existing.CurrentFileName;
        return await SaveForm(model, "Edit", token);
    }
    private async Task<IActionResult> SaveForm(MaterialForm model, string view, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model.Title)) ModelState.AddModelError(nameof(model.Title), "Please enter a material title.");
        MaterialFile? file = null;
        if (model.File != null)
        {
            try
            {
                var result = await MaterialFileService.ReadAsync(model.File, token);
                file = result.File;
                if (result.Error != null) ModelState.AddModelError(nameof(model.File), result.Error);
            }
            catch (IOException) { ModelState.AddModelError(nameof(model.File), "The file could not be read. Re-select it and try again."); }
        }
        else if (model.Id == 0) ModelState.AddModelError(nameof(model.File), "Choose a material file.");
        if (ModelState.IsValid)
        {
            var error = await Materials.SaveAsync(User, model, file);
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
    public async Task<IActionResult> ChangeStatus(int id, string? rowVersion, bool active)
    {
        if (!LearningMaterialService.IsTutor(User)) return Forbid();
        var error = ModelState.IsValid ? await Materials.StatusAsync(User, id, rowVersion, active) : "Check the status request and reload.";
        TempData[error == null ? "MaterialSuccess" : "MaterialError"] = error ?? (active ? "Learning material reactivated." : "Learning material set to inactive. Students can no longer access it.");
        return RedirectToAction(nameof(Index), new { status = "all" });
    }
}
