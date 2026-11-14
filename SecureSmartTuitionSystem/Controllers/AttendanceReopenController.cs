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
    [HttpGet] public async Task<IActionResult> Index()=>View(await new AttendanceReopenService(db).ListAsync(OwnId,User.IsInRole("Admin")));
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

