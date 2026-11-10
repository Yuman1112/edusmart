using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
namespace SecureSmartTuitionSystem.ViewComponents;
public sealed class EnrollmentNoticeStatusViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int applicationId)
    {
        if (!UserClaimsPrincipal.IsInRole("Admin")) return Content("");
        var rows = await db.EnrollmentNotices.AsNoTracking().Where(x => x.EnrollmentApplicationId == applicationId)
            .OrderBy(x => x.EnrollmentNoticeId).Select(x => new EnrollmentNotice {
                EnrollmentNoticeId = x.EnrollmentNoticeId, EnrollmentApplicationId = x.EnrollmentApplicationId,
                Kind = x.Kind, RecipientRole = x.RecipientRole, State = x.State, AttemptId = x.AttemptId,
                CreatedAtUtc = x.CreatedAtUtc, AttemptedAtUtc = x.AttemptedAtUtc, SentAtUtc = x.SentAtUtc, Note = x.Note }).ToListAsync();
        return View(rows);
    }
}
