using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TeachingRequestsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    AuditLogIntegrity auditIntegrity) : Controller
{
    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken token)
    {
        var profile = await OwnProfileAsync(token);
        return profile == null ? NotFound() : View(await PopulateMineAsync(new(), profile, token));
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Mine(TeachingChangeInput model, CancellationToken token)
    {
        var profile = await OwnProfileAsync(token);
        if (profile == null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(await PopulateMineAsync(model, profile, token));

        string level = model.AcademicLevel.Trim().ToUpperInvariant();
        string code = model.SubjectCode.Trim().ToUpperInvariant();
        if (!TeachingSubjectCatalog.IsValidSubject(level, code))
            ModelState.AddModelError(nameof(model.SubjectCode), "Select a subject available for this academic level.");

        var (file, uploadError) = await TutorQualificationService.ReadDocumentAsync(model.Evidence, token);
        if (uploadError != null)
            ModelState.AddModelError(nameof(model.Evidence), uploadError);
        if (!ModelState.IsValid)
            return View(await PopulateMineAsync(model, profile, token));

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var approvals = await LoadApprovalsAsync(profile.TutorProfileId, token);
            var type = model.Type!.Value;
            string? requestError = ValidateChange(type, level, code, approvals);
            if (requestError != null)
            {
                ModelState.AddModelError(nameof(model.SubjectCode), requestError);
                return View(await PopulateMineAsync(model, profile, token));
            }

            if (await db.TutorTeachingChangeRequests.AnyAsync(x => x.TutorProfileId == profile.TutorProfileId
                && x.AcademicLevel == level && x.SubjectCode == code && x.Status == TeachingChangeStatus.Pending, token))
            {
                ModelState.AddModelError(nameof(model.SubjectCode), "There is already a pending request for this subject and level.");
                return View(await PopulateMineAsync(model, profile, token));
            }

            var request = new TutorTeachingChangeRequest
            {
                TutorProfileId = profile.TutorProfileId,
                Type = type,
                AcademicLevel = level,
                SubjectCode = code,
                Reason = model.Reason.Trim(),
                SubmittedAtUtc = DateTime.UtcNow
            };
            db.TutorTeachingChangeRequests.Add(request);
            if (file != null)
            {
                db.TutorQualificationDocuments.Add(new TutorQualificationDocument
                {
                    TutorProfileId = profile.TutorProfileId,
                    TeachingChangeRequest = request,
                    Kind = TutorDocumentKind.RequestEvidence,
                    FileName = file.Name,
                    Content = file.Content
                });
            }
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (Exception exception) when (exception is DbUpdateException or System.Data.Common.DbException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "The request could not be saved. Check your requests before trying again.");
            return View(await PopulateMineAsync(model, profile, token));
        }

        TempData["TeachingSuccess"] = "Teaching change request submitted for review.";
        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await RequestRows(db.TutorTeachingChangeRequests)
            .OrderBy(x => x.Status == TeachingChangeStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.Id)
            .ToListAsync(token));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Review(int id, CancellationToken token)
    {
        var row = await FindRowAsync(id, token);
        return row == null ? NotFound() : View(new TeachingDecisionInput { Id = id, Request = row });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Review(TeachingDecisionInput model, bool? approve, CancellationToken token)
    {
        var adminId = users.GetUserId(User);
        if (adminId == null)
            return Challenge();

        model.Request = await FindRowAsync(model.Id, token);
        if (model.Request == null)
            return NotFound();
        if (approve == null)
            ModelState.AddModelError("", "Select Approve or Reject.");
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var request = await db.TutorTeachingChangeRequests
                .Include(x => x.TutorProfile)
                .SingleOrDefaultAsync(x => x.Id == model.Id, token);
            if (request == null)
                return NotFound();
            if (request.Status != TeachingChangeStatus.Pending)
            {
                TempData["TeachingWarning"] = "This request has already been reviewed.";
                return RedirectToAction(nameof(Review), new { id = model.Id });
            }

            if (approve == true)
            {
                var approvals = await LoadApprovalsAsync(request.TutorProfileId, token);
                var error = ValidateChange(request.Type, request.AcademicLevel, request.SubjectCode, approvals);
                if (error != null)
                    ModelState.AddModelError("", error + " Reload the request or reject it with an explanation.");
                if (request.Type != TeachingChangeType.Remove && !model.QualificationVerified)
                    ModelState.AddModelError(nameof(model.QualificationVerified),
                        "Confirm that you reviewed the qualification or supporting evidence.");
                if (!ModelState.IsValid)
                    return View(model);

                if (request.Type == TeachingChangeType.Remove)
                {
                    var removed = approvals.Where(item => TutorQualificationService.SameApproval(item,
                        request.AcademicLevel, request.SubjectCode)).ToList();
                    var conflict = await TutorQualificationService.FindRemovalConflictAsync(
                        db, request.TutorProfile.ApplicationUserId, removed, token);
                    if (conflict != null)
                    {
                        ModelState.AddModelError("", conflict);
                        return View(model);
                    }
                    db.TutorTeachingSubjects.RemoveRange(removed);
                }
                else
                {
                    db.TutorTeachingSubjects.Add(new TutorTeachingSubject
                    {
                        TutorProfileId = request.TutorProfileId,
                        EducationStage = TeachingSubjectCatalog.GetStageForLevel(request.AcademicLevel)!,
                        AcademicLevel = request.AcademicLevel,
                        SubjectCode = request.SubjectCode
                    });
                }

                request.TutorProfile.UpdatedAt = DateTime.UtcNow;
                TutorQualificationService.AddAudit(db, auditIntegrity, HttpContext,
                    request.TutorProfile.ApplicationUserId,
                    $"Request #{request.Id}: {(request.Type == TeachingChangeType.Remove ? "removed" : "added")} {request.AcademicLevel} / {request.SubjectCode}.");
            }

            request.Status = approve == true ? TeachingChangeStatus.Approved : TeachingChangeStatus.Rejected;
            request.ReviewNotes = model.ReviewNotes.Trim();
            request.ReviewedByUserId = adminId;
            request.ReviewedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (Exception exception) when (exception is DbUpdateException or System.Data.Common.DbException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "The decision could not be saved. Reload the request and try again.");
            return View(model);
        }

        TempData["TeachingSuccess"] = approve == true ? "Teaching change request approved." : "Teaching change request rejected.";
        return RedirectToAction(nameof(Review), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id, CancellationToken token)
    {
        string? userId = users.GetUserId(User);
        if (userId == null)
            return Challenge();

        bool admin = User.IsInRole("Admin");
        var document = await db.TutorQualificationDocuments.AsNoTracking()
            .Where(x => x.Id == id && (admin || (x.TeachingChangeRequestId != null
                && x.TutorProfile.ApplicationUserId == userId)))
            .Select(x => new { x.FileName, x.Content })
            .SingleOrDefaultAsync(token);

        if (document == null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(document.Content, "application/octet-stream", document.FileName);
    }

    private Task<TutorProfile?> OwnProfileAsync(CancellationToken token)
    {
        string? userId = users.GetUserId(User);
        return db.TutorProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ApplicationUserId == userId && x.IsActive
                && x.ApplicationUser.IsActive && !x.ApplicationUser.MustChangePassword, token);
    }

    private Task<List<TutorTeachingSubject>> LoadApprovalsAsync(int profileId, CancellationToken token) =>
        db.TutorTeachingSubjects.Where(x => x.TutorProfileId == profileId).ToListAsync(token);

    private async Task<TeachingChangeInput> PopulateMineAsync(
        TeachingChangeInput model, TutorProfile profile, CancellationToken token)
    {
        var approvals = await db.TutorTeachingSubjects.AsNoTracking()
            .Where(x => x.TutorProfileId == profile.TutorProfileId)
            .Select(x => new { x.AcademicLevel, x.SubjectCode })
            .ToListAsync(token);
        model.ApprovedSubjects = approvals.OrderBy(x => x.AcademicLevel).ThenBy(x => x.SubjectCode)
            .Select(x => $"{TeachingSubjectCatalog.GetSubjectDisplayName(x.SubjectCode)} · {x.AcademicLevel}")
            .ToList();
        model.Requests = await RequestRows(db.TutorTeachingChangeRequests.Where(x => x.TutorProfileId == profile.TutorProfileId))
            .OrderByDescending(x => x.Id)
            .ToListAsync(token);
        return model;
    }

    private Task<TeachingChangeRow?> FindRowAsync(int id, CancellationToken token) =>
        RequestRows(db.TutorTeachingChangeRequests.Where(x => x.Id == id)).SingleOrDefaultAsync(token);

    private IQueryable<TeachingChangeRow> RequestRows(IQueryable<TutorTeachingChangeRequest> requests) =>
        from request in requests.AsNoTracking()
        join document in db.TutorQualificationDocuments.Where(x => x.TeachingChangeRequestId != null)
            on (int?)request.Id equals document.TeachingChangeRequestId into evidence
        from document in evidence.DefaultIfEmpty()
        select new TeachingChangeRow
        {
            Id = request.Id,
            TutorId = request.TutorProfile.ApplicationUser.UserName ?? "",
            TutorName = request.TutorProfile.FullName,
            Type = request.Type,
            Status = request.Status,
            AcademicLevel = request.AcademicLevel,
            SubjectCode = request.SubjectCode,
            Reason = request.Reason,
            ReviewNotes = request.ReviewNotes,
            SubmittedAtUtc = request.SubmittedAtUtc,
            ReviewedAtUtc = request.ReviewedAtUtc,
            EvidenceId = document == null ? null : document.Id,
            EvidenceName = document == null ? null : document.FileName
        };

    private static string? ValidateChange(TeachingChangeType type, string level, string code,
        IReadOnlyCollection<TutorTeachingSubject> approvals)
    {
        if (!TeachingSubjectCatalog.IsValidSubject(level, code))
            return "This subject is not available for the selected academic level.";

        bool exact = approvals.Any(item => TutorQualificationService.SameApproval(item, level, code));
        bool alreadyTeachesSubject = approvals.Any(item =>
            string.Equals(item.SubjectCode?.Trim(), code, StringComparison.OrdinalIgnoreCase));

        return type switch
        {
            TeachingChangeType.Remove when !exact => "This subject and level are not currently approved.",
            TeachingChangeType.Remove when approvals.Count == 1 => "At least one teaching subject and level must remain approved.",
            TeachingChangeType.AddSubject or TeachingChangeType.AddLevel when exact => "This subject and level are already approved.",
            TeachingChangeType.AddSubject when alreadyTeachesSubject => "This subject is already approved at another level. Choose Add New Level.",
            TeachingChangeType.AddLevel when !alreadyTeachesSubject => "Choose Add New Subject for a subject you are not yet approved to teach.",
            TeachingChangeType.AddSubject or TeachingChangeType.AddLevel or TeachingChangeType.Remove => null,
            _ => "Select a valid request type."
        };
    }
}
