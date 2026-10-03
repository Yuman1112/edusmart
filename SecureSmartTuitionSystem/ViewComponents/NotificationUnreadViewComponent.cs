using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Enrollment;
namespace SecureSmartTuitionSystem.ViewComponents;
public sealed class NotificationUnreadViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var id = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == null || UserClaimsPrincipal.IsInRole("Admin")) return Content("");
        var today = ClassScheduleService.Today;
        return View(await db.UserNotifications.AsNoTracking().CountAsync(x => x.UserId == id && x.ReadAtUtc == null &&
            (x.AvailableFromDate == null || x.AvailableFromDate <= today)));
    }
}
