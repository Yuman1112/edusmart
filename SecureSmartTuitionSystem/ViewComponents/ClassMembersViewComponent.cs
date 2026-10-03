using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.ViewComponents;
public sealed class ClassMembersViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int classSessionId)
    {
        var id = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool admin = HttpContext.User.IsInRole("Admin");
        if (!admin && (!HttpContext.User.IsInRole("Tutor") || !await db.ClassSessions.AnyAsync(x => x.ClassSessionId == classSessionId && x.TutorUserId == id))) return Content("");
        return View(new ClassMembersModel { ClassSessionId = classSessionId, CanWithdraw = admin,
            Members = await db.StudentEnrollments.AsNoTracking().Include(x => x.StudentProfile).Where(x => x.ClassSessionId == classSessionId)
                .OrderBy(x => x.StudentProfile.StudentNumber).ThenByDescending(x => x.EnrolledAt).ToListAsync() });
    }
}
