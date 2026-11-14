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
using SecureSmartTuitionSystem.Services.Learning;
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
        AllowEvidencePreview();
        var profile = await OwnProfileAsync(token);
        return profile == null ? NotFound() : View(await PopulateMineAsync(new(), profile, token));
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    public async Task<IActionResult> Mine(TeachingChangeInput model, CancellationToken token)
    {
        AllowEvidencePreview();
        var profile = await OwnProfileAsync(token);
        if (profile == null)
            return NotFound();

        if (model.Type != TeachingChangeType.AddSubject)
            ModelState.AddModelError(nameof(model.Type), "Only requests to add subjects and levels are available.");
        if (!ModelState.IsValid)
            return View(await PopulateMineAsync(model, profile, token));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < model.Items.Count; index++)
        {
            var item = model.Items[index];
            item.AcademicLevel = item.AcademicLevel.Trim().ToUpperInvariant();
            item.SubjectCode = item.SubjectCode.Trim().ToUpperInvariant();
            if (!TeachingSubjectCatalog.IsValidSubject(item.AcademicLevel, item.SubjectCode))
                ModelState.AddModelError($"Items[{index}].SubjectCode", "Select a subject available for this academic level.");
            if (!seen.Add(item.AcademicLevel + "/" + item.SubjectCode))
                ModelState.AddModelError($"Items[{index}].SubjectCode", "This subject and level is already in this submission.");
        }
        if (!ModelState.IsValid)
            return View(await PopulateMineAsync(model, profile, token));

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var approvals = await LoadApprovalsAsync(profile.TutorProfileId, token);
            var pending = await db.TutorTeachingChangeRequests
                .Where(x => x.TutorProfileId == profile.TutorProfileId && x.Status == TeachingChangeStatus.Pending)
                .Select(x => new { x.AcademicLevel, x.SubjectCode }).ToListAsync(token);
            for (int index = 0; index < model.Items.Count; index++)
            {
                var item = model.Items[index];
                var error = ValidateChange(TeachingChangeType.AddSubject, item.AcademicLevel, item.SubjectCode, approvals);
                if (error != null)
                    ModelState.AddModelError($"Items[{index}].SubjectCode", error);
                if (pending.Any(x => x.AcademicLevel == item.AcademicLevel && x.SubjectCode == item.SubjectCode))
                    ModelState.AddModelError($"Items[{index}].SubjectCode", "There is already a pending request for this subject and level.");
            }
            var files = await ReadEvidenceAsync(model, profile.TutorProfileId, token);
            if (!ModelState.IsValid)
                return View(await PopulateMineAsync(model, profile, token));

            TutorTeachingChangeRequest? evidenceRequest = null;
            foreach (var item in model.Items)
            {
                var request = new TutorTeachingChangeRequest
                {
                    TutorProfileId = profile.TutorProfileId,
                    Type = TeachingChangeType.AddSubject,
                    AcademicLevel = item.AcademicLevel,
                    SubjectCode = item.SubjectCode,
                    Reason = model.Reason.Trim(),
                    SubmittedAtUtc = DateTime.UtcNow,
                    EvidenceRequest = evidenceRequest
                };
                db.TutorTeachingChangeRequests.Add(request);
                evidenceRequest ??= request;
            }
            foreach (var file in files)
            {
                db.TutorQualificationDocuments.Add(new TutorQualificationDocument
                {
                    TutorProfileId = profile.TutorProfileId,
                    TeachingChangeRequest = evidenceRequest,
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

        TempData["TeachingSuccess"] = $"{model.Items.Count} subject and level request(s) submitted. Each item will be reviewed separately.";
        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await RequestRows(db.TutorTeachingChangeRequests)
            .OrderBy(x => x.Status == TeachingChangeStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.Id)
            .ToListAsync(token));

    [HttpGet]
    public async Task<IActionResult> Live(CancellationToken token)
    {
        if (User.IsInRole("Admin"))
        {
            return PartialView("_LiveState", new TeachingRequestListViewModel
            {
                IsAdmin = true,
                Requests = await RequestRows(db.TutorTeachingChangeRequests)
                    .OrderBy(x => x.Status == TeachingChangeStatus.Pending ? 0 : 1)
                    .ThenByDescending(x => x.Id).ToListAsync(token)
            });
        }

        var profile = await OwnProfileAsync(token);
        if (profile == null)
            return Forbid();
        var mine = await PopulateMineAsync(new TeachingChangeInput(), profile, token);
        return PartialView("_LiveState", new TeachingRequestListViewModel
        {
            Requests = mine.Requests,
            ApprovedSubjects = mine.ApprovedSubjects,
            UnavailableCombinations = mine.UnavailableCombinations
        });
    }

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
                if (request.Type == TeachingChangeType.Remove)
                {
                    ModelState.AddModelError("", "Subject removal requests are currently unavailable. You can reject this earlier request with an explanation.");
                    return View(model);
                }
                var approvals = await LoadApprovalsAsync(request.TutorProfileId, token);
                var error = ValidateChange(request.Type, request.AcademicLevel, request.SubjectCode, approvals);
                if (error != null)
                    ModelState.AddModelError("", error + " Reload the request or reject it with an explanation.");
                if (!model.QualificationVerified)
                    ModelState.AddModelError(nameof(model.QualificationVerified),
                        "Confirm that you reviewed the qualification or supporting evidence.");
                if (!ModelState.IsValid)
                    return View(model);

                db.TutorTeachingSubjects.Add(new TutorTeachingSubject
                {
                    TutorProfileId = request.TutorProfileId,
                    EducationStage = TeachingSubjectCatalog.GetStageForLevel(request.AcademicLevel)!,
                    AcademicLevel = request.AcademicLevel,
                    SubjectCode = request.SubjectCode
                });

                request.TutorProfile.UpdatedAt = DateTime.UtcNow;
                TutorQualificationService.AddAudit(db, auditIntegrity, HttpContext,
                    request.TutorProfile.ApplicationUserId,
                    $"Request #{request.Id}: added {request.AcademicLevel} / {request.SubjectCode}.");
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

    private void AllowEvidencePreview()
    {
        var policy = Response.Headers["Content-Security-Policy"].ToString();
        if (policy.Length == 0)
            policy = "default-src 'self'; img-src 'self' data:;";
        var directives = policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        directives.RemoveAll(directive => directive.StartsWith("frame-src ", StringComparison.OrdinalIgnoreCase));
        var imageIndex = directives.FindIndex(directive => directive.StartsWith("img-src ", StringComparison.OrdinalIgnoreCase));
        if (imageIndex < 0)
            directives.Add("img-src 'self' data: blob:");
        else if (!directives[imageIndex].Split(' ').Contains("blob:"))
            directives[imageIndex] += " blob:";
        directives.Add("frame-src 'self' blob:");
        Response.Headers["Content-Security-Policy"] = string.Join("; ", directives) + ";";
    }

    private Task<TutorProfile?> OwnProfileAsync(CancellationToken token)
    {
        string? userId = users.GetUserId(User);
        return db.TutorProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ApplicationUserId == userId && x.IsActive
                && x.ApplicationUser.IsActive && !x.ApplicationUser.MustChangePassword, token);
    }

    private async Task<List<MaterialFile>> ReadEvidenceAsync(
        TeachingChangeInput model, int profileId, CancellationToken token)
    {
        const long totalLimit = 25L * 1024 * 1024;
        var uploads = model.Evidence ?? [];
        var files = new List<MaterialFile>();
        var selectedIds = model.ReferenceDocumentIds.Distinct().ToList();
        if (selectedIds.Count != model.ReferenceDocumentIds.Count
            || uploads.Count + selectedIds.Count > 5)
        {
            ModelState.AddModelError(nameof(model.Evidence), "Choose at most five files in total, including existing documents.");
            return files;
        }

        var existing = selectedIds.Count == 0 ? new List<TutorQualificationDocument>()
            : await db.TutorQualificationDocuments.AsNoTracking()
            .Where(x => selectedIds.Contains(x.Id) && x.TutorProfileId == profileId
                && x.TeachingChangeRequestId == null && x.TutorProfile.QualificationVerifiedAtUtc != null)
            .ToListAsync(token);
        if (existing.Count != selectedIds.Count)
        {
            ModelState.AddModelError(nameof(model.ReferenceDocumentIds), "An existing document is no longer available or verified. Select the documents again.");
            return files;
        }

        long size = existing.Sum(x => (long)x.Content.Length);
        foreach (var upload in uploads)
        {
            if (upload.Length < 1 || upload.Length > MaterialFileService.MaxBytes)
                ModelState.AddModelError(nameof(model.Evidence), "Each uploaded file must be non-empty and no larger than 10 MB.");
            size += upload.Length;
        }
        if (size > totalLimit)
            ModelState.AddModelError(nameof(model.Evidence), "The combined evidence must not exceed 25 MB.");
        if (!ModelState.IsValid)
            return files;

        foreach (var document in existing)
        {
            var type = MaterialFileService.PreviewContentType(document.FileName);
            if (type == null || !MaterialFileService.Matches(Path.GetExtension(document.FileName), document.Content))
                ModelState.AddModelError(nameof(model.ReferenceDocumentIds), "Choose valid PDF, PNG or JPG evidence.");
            else
                files.Add(new MaterialFile(document.FileName, type, document.Content));
        }
        foreach (var upload in uploads)
        {
            var (file, error) = await TutorQualificationService.ReadDocumentAsync(upload, token);
            if (error != null)
                ModelState.AddModelError(nameof(model.Evidence), error);
            else if (file != null)
                files.Add(file);
        }
        return files;
    }

    private Task<List<TutorTeachingSubject>> LoadApprovalsAsync(int profileId, CancellationToken token) =>
        db.TutorTeachingSubjects.Where(x => x.TutorProfileId == profileId).ToListAsync(token);

    private async Task<TeachingChangeInput> PopulateMineAsync(
        TeachingChangeInput model, TutorProfile profile, CancellationToken token)
    {
        if (model.Items.Count == 0)
            model.Items.Add(new TeachingSubjectInput());
        model.ExistingEvidence = await db.TutorQualificationDocuments.AsNoTracking()
            .Where(x => x.TutorProfileId == profile.TutorProfileId
                && x.TeachingChangeRequestId == null && x.TutorProfile.QualificationVerifiedAtUtc != null)
            .Select(x => new TeachingEvidenceRow { Id = x.Id, Name = x.FileName, Size = x.Content.Length })
            .ToListAsync(token);
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
        model.UnavailableCombinations = approvals.Select(x => x.AcademicLevel.Trim().ToUpperInvariant() + "/" + x.SubjectCode.Trim().ToUpperInvariant())
            .Concat(model.Requests.Where(x => x.Status == TeachingChangeStatus.Pending)
                .Select(x => x.AcademicLevel.Trim().ToUpperInvariant() + "/" + x.SubjectCode.Trim().ToUpperInvariant()))
            .Distinct().ToList();
        return model;
    }

    private Task<TeachingChangeRow?> FindRowAsync(int id, CancellationToken token) =>
        RequestRows(db.TutorTeachingChangeRequests.Where(x => x.Id == id)).SingleOrDefaultAsync(token);

    private IQueryable<TeachingChangeRow> RequestRows(IQueryable<TutorTeachingChangeRequest> requests) =>
        requests.AsNoTracking().Select(request => new TeachingChangeRow
        {
            Id = request.Id,
            SubmissionId = request.EvidenceRequestId ?? request.Id,
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
            Evidence = db.TutorQualificationDocuments
                .Where(document => document.TutorProfileId == request.TutorProfileId
                    && document.TeachingChangeRequestId == (request.EvidenceRequestId ?? request.Id))
                .OrderBy(document => document.Id)
                .Select(document => new TeachingEvidenceRow
                {
                    Id = document.Id, Name = document.FileName, Size = document.Content.Length
                }).ToList()
        });

    private static string? ValidateChange(TeachingChangeType type, string level, string code,
        IReadOnlyCollection<TutorTeachingSubject> approvals)
    {
        if (!TeachingSubjectCatalog.IsValidSubject(level, code))
            return "This subject is not available for the selected academic level.";

        bool exact = approvals.Any(item => TutorQualificationService.SameApproval(item, level, code));
        return type switch
        {
            TeachingChangeType.AddSubject or TeachingChangeType.AddLevel when exact => "This subject and level are already approved.",
            TeachingChangeType.AddSubject or TeachingChangeType.AddLevel => null,
            _ => "Select a valid request type."
        };
    }
}
