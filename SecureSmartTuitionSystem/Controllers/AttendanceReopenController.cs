using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Attendance;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles="Admin,Tutor"),AutoValidateAntiforgeryToken,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AttendanceReopenController(ApplicationDbContext db,UserManager<ApplicationUser> users):Controller
{
    private string OwnId=>users.GetUserId(User)!;
    [HttpGet] public async Task<IActionResult> Index(string? status, string? attempts)
    {
        status = status is "Pending" or "Approved" or "Rejected" ? status : "All";
        ViewData["Status"] = status;
        attempts = attempts is "First" or "Repeat" ? attempts : "All";
        ViewData["Attempts"] = attempts;
        var service = new AttendanceReopenService(db);
        var numbers = await service.AttemptNumbersAsync(OwnId,User.IsInRole("Admin"));
        ViewData["AttemptNumbers"] = numbers;
        var rows = await service.ListAsync(OwnId,User.IsInRole("Admin"));
        return View(rows.Where(r => (status == "All" || r.Decision.ToString() == status) &&
            (attempts == "All" || (attempts == "First" ? numbers[r.AttendanceReopenRequestId] == 1 : numbers[r.AttendanceReopenRequestId] > 1))).ToList());
    }
    [HttpGet,Authorize(Roles="Admin")]
    public async Task<IActionResult> History(int id)
    {
        var model = await new AttendanceReopenService(db).HistoryAsync(id);
        return model == null ? NotFound() : PartialView("_RequestHistory",model);
    }
    [HttpPost,Authorize(Roles="Tutor")]
    public async Task<IActionResult> Apply(int classId,DateTime date,int? changeId,bool replacement,string? reason)
    {
        if(User.IsInRole("Admin"))return Forbid();
        if(!ModelState.IsValid||date.Year is <2000 or >2100||classId<=0)return BadRequest();
        string? error;
        try{error=await new AttendanceReopenService(db).RequestAsync(classId,date,changeId,replacement,reason,OwnId);}
        catch(Exception ex) when(ex is DbUpdateException || ex is SqlException sql && sql.Number==1205){db.ChangeTracker.Clear();error="Another request changed this lesson. Reload and try again.";}
        TempData[error==null?"AttendanceReopenSuccess":"AttendanceReopenError"]=error??"Reopening request submitted. Attendance remains locked until approval.";
        return error==null?RedirectToAction(nameof(Index)):RedirectToAction("Sheet","Attendance",new{classId,date=date.ToString("yyyy-MM-dd"),changeId,replacement});
    }
    [HttpPost,Authorize(Roles="Admin")]
    public async Task<IActionResult> Review(int id,string decision,string? remark,string? rowVersion)
    {
        if(!ModelState.IsValid || decision is not("approve" or "reject"))return BadRequest();
        string? error;
        try{error=await new AttendanceReopenService(db).ReviewAsync(id,decision=="approve",remark,rowVersion,OwnId);}
        catch(Exception ex) when(ex is DbUpdateException || ex is SqlException sql && sql.Number==1205){db.ChangeTracker.Clear();error="Another request changed this review. Reload and try again.";}
        TempData[error==null?"AttendanceReopenSuccess":"AttendanceReopenError"]=error??"Review saved. Approval opens a 24-hour entry window for the assigned tutor.";
        return RedirectToAction(nameof(Index));
    }
}

