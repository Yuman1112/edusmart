using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Admin")]
public sealed class EnrollmentNoticesController(ApplicationDbContext db) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Recover(long id, Guid? attemptId, string outcome, bool verified)
    {
        var row = await db.EnrollmentNotices.AsNoTracking().SingleOrDefaultAsync(x => x.EnrollmentNoticeId == id);
        if (row == null) return NotFound();
        if (!verified || outcome is not ("sent" or "not-sent")) return BadRequest();
        var query = db.EnrollmentNotices.Where(x => x.EnrollmentNoticeId == id && x.AttemptId == attemptId &&
            (x.State == EnrollmentNoticeState.Unconfirmed || x.State == EnrollmentNoticeState.Failed));
        var nextAttempt = Guid.NewGuid();
        var affected = outcome == "sent" ?
            await query.ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Sent)
                .SetProperty(x => x.SentAtUtc, DateTime.UtcNow).SetProperty(x => x.Note, "Administrator verified prior sending; time shown is recovery-record time.")
                .SetProperty(x => x.AttemptId, nextAttempt)) :
            await query.ExecuteUpdateAsync(s => s.SetProperty(x => x.State, EnrollmentNoticeState.Pending)
                .SetProperty(x => x.Note, "Administrator verified no prior sending and requested a retry.")
                .SetProperty(x => x.AttemptId, nextAttempt));
        TempData[affected == 1 ? "SuccessMessage" : "ErrorMessage"] = affected == 1 ? "Email recovery recorded." : "This email changed. Refresh and review its current state.";
        return RedirectToAction("Details", "AdminEnrollments", new { id = row.EnrollmentApplicationId });
    }
}
