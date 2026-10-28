using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using Stripe;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Admin,Parent,Student")]
public sealed class EnrollmentExitsController(ApplicationDbContext db, EnrollmentExitService exits, StripeClient stripe,
    ILogger<EnrollmentExitsController> logger) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<EnrollmentExitRequest> Allowed => db.EnrollmentExitRequests.Where(x => exits.Accessible(User).Select(e => e.StudentEnrollmentId).Contains(x.StudentEnrollmentId));
    [HttpGet] public async Task<IActionResult> Index(int? studentProfileId = null,
        EducationStage? stage = null, string? level = null, string? subject = null, ExitRefundStatus? refundStatus = null)
    {
        var admin = User.IsInRole("Admin");
        var enrollments = exits.Accessible(User);
        if (studentProfileId.HasValue && !await enrollments.AnyAsync(x => x.StudentProfileId == studentProfileId)) return NotFound();
        var history = studentProfileId.HasValue ? Allowed.Where(x => x.StudentEnrollment.StudentProfileId == studentProfileId) : Allowed;
        ViewBag.Enrollments = admin ? new List<StudentEnrollment>() : await enrollments.AsNoTracking()
            .Include(x => x.StudentProfile).Include(x => x.ClassSession)
            .Where(x => x.Status == StudentEnrollmentStatus.Active).OrderBy(x => x.StudentProfile.StudentNumber).ThenBy(x => x.StartDate).ToListAsync();
        if (admin)
        {
            if (stage.HasValue && !Enum.IsDefined(stage.Value)) stage = null;
            level = level?.Trim().ToUpperInvariant();
            var levels = new List<string>();
            if (!stage.HasValue || stage == EducationStage.Primary) levels.AddRange(Enumerable.Range(1, 6).Select(n => $"YEAR {n}"));
            if (!stage.HasValue || stage == EducationStage.Secondary) levels.AddRange(Enumerable.Range(1, 5).Select(n => $"FORM {n}"));
            if (!levels.Contains(level ?? "")) level = null;
            if (stage.HasValue) history = history.Where(x => x.StudentEnrollment.ClassSession.Subject.EducationStage == stage.Value);
            if (level != null) history = history.Where(x => x.StudentEnrollment.ClassSession.Subject.AcademicLevel == level);
            var subjects = await history.Select(x => x.StudentEnrollment.ClassSession.Subject.SubjectName).Distinct().OrderBy(x => x).ToListAsync();
            if (!subjects.Contains(subject ?? "")) subject = null;
            if (subject != null) history = history.Where(x => x.StudentEnrollment.ClassSession.Subject.SubjectName == subject);
            if (refundStatus.HasValue && !Enum.IsDefined(refundStatus.Value)) refundStatus = null;
            if (refundStatus.HasValue) history = history.Where(x => x.RefundStatus == refundStatus.Value);
            ViewBag.Stage = stage?.ToString() ?? "";
            ViewBag.Level = level ?? "";
            ViewBag.SubjectFilter = subject ?? "";
            ViewBag.RefundStatusFilter = refundStatus?.ToString() ?? "";
            ViewBag.LevelOptions = levels;
            ViewBag.SubjectOptions = subjects;
        }
        ViewBag.StudentProfileId = studentProfileId;
        return View(await history.AsNoTracking().Include(x => x.StudentEnrollment).ThenInclude(x => x.StudentProfile)
            .Include(x => x.StudentEnrollment).ThenInclude(x => x.ClassSession)
            .OrderByDescending(x => x.RequestedAtUtc).ThenByDescending(x => x.EnrollmentExitRequestId).ToListAsync());
    }
    [Authorize(Roles = "Admin,Parent,Student"), HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Stop(int enrollmentId, string reason, bool confirmed)
    {
        if (User.IsInRole("Admin")) return Forbid();
        if (!confirmed) return BadRequest();
        try { var request = await exits.StopAsync(User, enrollmentId, reason); TempData["SuccessMessage"] = $"Withdrawal recorded for {request.EffectiveDate:dd MMM yyyy}. Estimated eligible tuition refund: RM {request.EstimatedRefund:F2}; this is not a completed refund."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> Details(int id)
    {
        var request = await Allowed.AsNoTracking().Include(x => x.StudentEnrollment).ThenInclude(x => x.StudentProfile)
            .Include(x => x.StudentEnrollment).ThenInclude(x => x.ClassSession)
            .Include(x => x.Payment).ThenInclude(x => x!.Bill).FirstOrDefaultAsync(x => x.EnrollmentExitRequestId == id);
        if (request == null) return NotFound();
        ViewBag.CurrentQuote = (await exits.QuoteAsync(request.StudentEnrollment, request.EffectiveDate, id)).Amount;
        return View(request);
    }
    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, string? note)
    {
        if (note is { Length: > 500 }) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await db.EnrollmentExitRequests.FirstOrDefaultAsync(x => x.EnrollmentExitRequestId == id);
        if (request == null) return NotFound();
        // A blank optional note must not erase an existing refund audit explanation.
        request.ReviewNote = string.IsNullOrWhiteSpace(note)
            ? request.ReviewNote ?? "Withdrawal record reviewed by Admin."
            : note.Trim();
        request.ReviewedByUserId = UserId;
        await db.SaveChangesAsync(); await tx.CommitAsync();
        TempData["SuccessMessage"] = "Review note saved. Withdrawal and refund status are unchanged.";
        return RedirectToAction(nameof(Details), new { id });
    }
    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(int id, string? note, bool confirmed)
    {
        if (!confirmed || note is { Length: > 500 }) return BadRequest();
        string refundNote = string.IsNullOrWhiteSpace(note)
            ? "Refund of unused future prepaid tuition based on the server-calculated amount and original payment."
            : note.Trim();
        EnrollmentExitRequest request;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var found = await db.EnrollmentExitRequests.Include(x => x.StudentEnrollment).Include(x => x.Payment).FirstOrDefaultAsync(x => x.EnrollmentExitRequestId == id);
            if (found == null) return NotFound();
            request = found;
            if (request.RefundStatus == ExitRefundStatus.Processing)
            {
                // An uncertain outcome must never be retried after the idempotency safety window.
                if (request.ProviderRefundId != null || !request.RefundStartedAtUtc.HasValue || request.RefundStartedAtUtc.Value < DateTime.UtcNow.AddHours(-12))
                { TempData["ErrorMessage"] = "Verify the refund in Stripe and reconcile its refund ID before retrying."; return RedirectToAction(nameof(Details), new { id }); }
            }
            else if (request.RefundStatus == ExitRefundStatus.PendingReview)
            {
                var quote = await exits.QuoteAsync(request.StudentEnrollment, request.EffectiveDate, id);
                if (quote.Payment == null || quote.Amount <= 0m || string.IsNullOrWhiteSpace(quote.Payment.ProviderPaymentIntentId))
                { TempData["ErrorMessage"] = "No eligible prepaid tuition or verified Stripe payment intent is available. Check the original payment."; return RedirectToAction(nameof(Details), new { id }); }
                request.ApprovedRefund = Math.Min(request.EstimatedRefund, quote.Amount); request.RefundStatus = ExitRefundStatus.Processing;
                request.RefundStartedAtUtc = DateTime.UtcNow; request.ReviewNote = refundNote; request.ReviewedByUserId = UserId;
            }
            else return BadRequest();
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        try
        {
            var result = await new RefundService(stripe).CreateAsync(new RefundCreateOptions {
                PaymentIntent = request.Payment!.ProviderPaymentIntentId, Amount = checked((long)decimal.Round(request.ApprovedRefund * 100m, 0)), Reason = "requested_by_customer",
                Metadata = new Dictionary<string, string> { ["exitRequestId"] = id.ToString(), ["enrollmentId"] = request.StudentEnrollmentId.ToString() } },
                new RequestOptions { IdempotencyKey = $"edusmart-exit-refund-{id}" }, HttpContext.RequestAborted);
            await ApplyProviderResult(request, result);
            TempData["SuccessMessage"] = "Refund submitted; check its provider status below.";
        }
        catch (Exception ex) when (ex is StripeException || ex is HttpRequestException || ex is TaskCanceledException)
        {
            logger.LogError(ex, "Refund outcome requires verification for request {RequestId}.", id);
            await db.Entry(request).ReloadAsync(); request.LastError = "Provider outcome is not confirmed. Check Stripe before retrying; never make a second manual refund for the same request.";
            await db.SaveChangesAsync(); TempData["ErrorMessage"] = request.LastError;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(int id, string? refundId)
    {
        var request = await db.EnrollmentExitRequests.Include(x => x.Payment).FirstOrDefaultAsync(x => x.EnrollmentExitRequestId == id);
        if (request == null) return NotFound();
        string? providerId = request.ProviderRefundId ?? refundId?.Trim();
        if (string.IsNullOrWhiteSpace(providerId) || providerId.Length > 255) return BadRequest();
        try
        {
            var result = await new RefundService(stripe).GetAsync(providerId, cancellationToken: HttpContext.RequestAborted);
            if (request.Payment == null || result.PaymentIntentId != request.Payment.ProviderPaymentIntentId || result.Amount != checked((long)decimal.Round(request.ApprovedRefund * 100m, 0)) || !result.Currency.Equals(request.Payment.Currency, StringComparison.OrdinalIgnoreCase) ||
                !result.Metadata.TryGetValue("exitRequestId", out string? source) || source != id.ToString()) return BadRequest();
            await ApplyProviderResult(request, result);
        }
        catch (StripeException ex) { logger.LogError(ex, "Refund refresh failed."); TempData["ErrorMessage"] = "Could not verify refund status. Retry after checking Stripe connectivity."; }
        return RedirectToAction(nameof(Details), new { id });
    }
    private async Task ApplyProviderResult(EnrollmentExitRequest request, Refund refund)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Entry(request).ReloadAsync();
        request.ProviderRefundId = refund.Id; request.LastError = null;
        request.RefundStatus = refund.Status == "succeeded" ? ExitRefundStatus.Completed : refund.Status is "failed" or "canceled" ? ExitRefundStatus.Failed : ExitRefundStatus.PendingProvider;
        if (request.RefundStatus == ExitRefundStatus.Completed) request.RefundCompletedAtUtc ??= DateTime.UtcNow;
        if (request.RefundStatus == ExitRefundStatus.Failed) request.LastError = "The provider reports an unsuccessful refund. Admin must reconcile the original payment before any further refund.";
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
