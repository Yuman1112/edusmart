using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
namespace SecureSmartTuitionSystem.ViewComponents;
public sealed class GuestEnquiryPendingViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() => !UserClaimsPrincipal.IsInRole("Admin") ? Content("") :
        View(await db.GuestEnquiries.AsNoTracking().CountAsync(x => !x.IsResolved));
}
