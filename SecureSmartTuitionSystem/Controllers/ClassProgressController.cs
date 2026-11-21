using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Progress;
using SecureSmartTuitionSystem.ViewModels.Progress;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles="Admin,Tutor")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ClassProgressController(ApplicationDbContext db):Controller
{
    private ClassProgressService Service=>new(db);
    [HttpGet] public async Task<IActionResult> Index([FromQuery]ProgressFilter filter)
    { if(!ModelState.IsValid)return BadRequest("Choose a valid year, month, subject and class.");return View(await Service.IndexAsync(User,filter)); }
    [HttpGet] public async Task<IActionResult> Details(int id,int? month=null,DateTime? date=null,int key=0,int historyPage=1)
    {
        if(!ModelState.IsValid||id<1||month is <1 or >12||key<0||historyPage is <1 or >10000||date?.Year is <2000 or >2100)return BadRequest("Choose a valid lesson.");
        var model=await Service.DetailsAsync(User,id,date?.Month??month??DateTime.UtcNow.AddHours(8).Month,date,key,historyPage);return model==null?NotFound():View(model);
    }
    [HttpGet,Authorize(Roles="Admin")] public async Task<IActionResult> Syllabus(int id)
    {if(!ModelState.IsValid||id<1)return BadRequest();var m=await Service.SyllabusAsync(User,id);return m==null?NotFound():View(m);}
    [HttpPost,Authorize(Roles="Admin"),RequestSizeLimit(262144)] public async Task<IActionResult> Syllabus(SyllabusForm form)
    {
        if(!ModelState.IsValid)return BadRequest("Check chapter names, tracking date and outline version.");
        var error=await Service.SaveSyllabusAsync(User,form);TempData[error==null?"ProgressSuccess":"ProgressError"]=error??"Subject outline saved.";
        return RedirectToAction(nameof(Syllabus),new{id=form.ClassId});
    }
    [HttpPost,Authorize(Roles="Tutor"),RequestSizeLimit(262144)] public async Task<IActionResult> Save(LessonProgressForm form)
    {
        if(User.IsInRole("Admin"))return Forbid();
        if(!ModelState.IsValid)return BadRequest("Check the lesson, summary and progress values.");
        if(!await Service.Classes(User).AnyAsync(c=>c.ClassSessionId==form.ClassId))return NotFound();
        var error=await Service.SaveLessonAsync(User,form);TempData[error==null?"ProgressSuccess":"ProgressError"]=error??"Teaching progress saved. Completed subchapters are locked.";
        return RedirectToAction(nameof(Details),new{id=form.ClassId,date=form.Date.ToString("yyyy-MM-dd"),key=form.ChangeKey});
    }
    [HttpPost,Authorize(Roles="Admin")] public async Task<IActionResult> Reopen(ReopenProgressForm form)
    {
        if(!ModelState.IsValid)return BadRequest("Enter the correction reason and reload the latest progress.");
        var error=await Service.ReopenAsync(User,form);TempData[error==null?"ProgressSuccess":"ProgressError"]=error??"Progress corrected. The original values and reason are retained in history.";
        return RedirectToAction(nameof(Details),new{id=form.ClassId});
    }
}
