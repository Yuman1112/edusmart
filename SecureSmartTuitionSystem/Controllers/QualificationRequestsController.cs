using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class QualificationRequestsController(ApplicationDbContext db,
    UserManager<ApplicationUser> users, AuditLogIntegrity audit) : Controller
{
    private IQueryable<TutorProfile> Own() => db.TutorProfiles
        .Where(p => p.ApplicationUserId == users.GetUserId(User) && p.IsActive && p.ApplicationUser.IsActive);
    private static IQueryable<QualificationUpdateRow> Rows(IQueryable<TutorQualificationUpdateRequest> query) =>
        query.Select(r => new QualificationUpdateRow {
            Id=r.Id, TutorId=r.TutorProfile.ApplicationUser.UserName ?? "", TutorName=r.TutorProfile.FullName,
            Qualification=r.Qualification, FieldOfStudy=r.FieldOfStudy, AreaOfExpertise=r.AreaOfExpertise,
            OriginalQualification=r.OriginalQualification, OriginalFieldOfStudy=r.OriginalFieldOfStudy, OriginalAreaOfExpertise=r.OriginalAreaOfExpertise,
            CertificateName=r.CertificateName, CertificateSize=r.CertificateContent.LongLength,
            AdditionalFiles=r.AdditionalFiles.OrderBy(f=>f.Id).Select(f=>new QualificationFileSummary(f.Id,f.FileName,f.Content.LongLength)).ToList(), Reason=r.Reason, Status=r.Status,
            SubmittedAtUtc=r.SubmittedAtUtc, ReviewedAtUtc=r.ReviewedAtUtc, ReviewNotes=r.ReviewNotes });
    private async Task<QualificationUpdateInput> Populate(QualificationUpdateInput model, int tutorId, CancellationToken token)
    {
        var current = await db.TutorProfiles.AsNoTracking().SingleAsync(p => p.TutorProfileId == tutorId, token);
        model.BaselineToken = BaselineProtector.Protect(JsonSerializer.Serialize(new QualificationBaseline(current.ApplicationUserId,
            current.Qualification, current.FieldOfStudy, current.AreaOfExpertise, current.QualificationVerifiedAtUtc, DateTime.UtcNow)));
        ModelState.Remove(nameof(model.BaselineToken));
        model.CurrentQualification = current.Qualification;
        model.CurrentFieldOfStudy = current.FieldOfStudy;
        model.CurrentAreaOfExpertise = current.AreaOfExpertise;
        model.CurrentVerified = current.QualificationVerifiedAtUtc.HasValue;
        model.Requests = await Rows(db.TutorQualificationUpdateRequests.AsNoTracking().Where(r=>r.TutorProfileId==tutorId))
            .OrderByDescending(r=>r.Id).Take(100).ToListAsync(token);
        return model;
    }

    [Authorize(Roles="Tutor"), HttpGet]
    public async Task<IActionResult> Mine(CancellationToken token, string? search=null, QualificationUpdateStatus? status=null, DateTime? from=null, DateTime? to=null)
    {
        var p=await Own().AsNoTracking().SingleOrDefaultAsync(token);
        if(p==null) return NotFound();
        SetFilters(search,status,from,to);
        ViewData["QualificationHasPending"]=await db.TutorQualificationUpdateRequests.AnyAsync(r=>r.TutorProfileId==p.TutorProfileId && r.Status==QualificationUpdateStatus.Pending,token);
        return View(await Rows(Filter(db.TutorQualificationUpdateRequests.AsNoTracking().Where(r=>r.TutorProfileId==p.TutorProfileId),search,status,from,to)).OrderByDescending(r=>r.Id).Take(100).ToListAsync(token));
    }

    [Authorize(Roles="Tutor"), HttpGet]
    public async Task<IActionResult> Create(CancellationToken token)
    {
        var p=await Own().AsNoTracking().SingleOrDefaultAsync(token);
        if(p==null) return NotFound();
        if(await db.TutorQualificationUpdateRequests.AnyAsync(r=>r.TutorProfileId==p.TutorProfileId && r.Status==QualificationUpdateStatus.Pending,token)) {
            TempData["QualificationSuccess"]="You have a pending request. View or cancel it before starting another.";
            return RedirectToAction(nameof(Mine));
        }
        return View(await Populate(new() { Qualification=p.Qualification, FieldOfStudy=p.FieldOfStudy ?? "",
            AreaOfExpertise=p.AreaOfExpertise ?? "" },p.TutorProfileId,token));
    }

    [Authorize(Roles="Tutor"), HttpPost, RequestSizeLimit(27*1024*1024)]
    [RequestFormLimits(MultipartBodyLengthLimit=27*1024*1024)]
    public async Task<IActionResult> Mine(QualificationUpdateInput model, CancellationToken token)
    {
        var p=await Own().SingleOrDefaultAsync(token);
        if(p==null) return NotFound();
        if(await db.TutorQualificationUpdateRequests.AnyAsync(r=>r.TutorProfileId==p.TutorProfileId && r.Status==QualificationUpdateStatus.Pending,token))
            ModelState.AddModelError("", "You already have a pending qualification request. Cancel it before submitting another.");
        if (!BaselineMatches(model.BaselineToken, p))
            ModelState.AddModelError("", "Your current qualification details changed or this form expired. Compare the current and proposed details, then submit again.");
        var uploads = model.Certificates.ToList();
        if(model.Certificate != null) uploads.Insert(0, model.Certificate);
        if(uploads.Count > 5) ModelState.AddModelError(nameof(model.Certificates),"Select up to 5 files.");
        if(uploads.Sum(f=>f.Length) > 25L*1024*1024) ModelState.AddModelError(nameof(model.Certificates),"The total file size must not exceed 25 MB.");
        bool qualificationChanged = !QualificationChangeRules.Same(p.Qualification, model.Qualification);
        bool fieldChanged = !QualificationChangeRules.Same(p.FieldOfStudy, model.FieldOfStudy);
        bool expertiseChanged = !QualificationChangeRules.Same(p.AreaOfExpertise, model.AreaOfExpertise);
        if (!string.IsNullOrWhiteSpace(p.FieldOfStudy) && string.IsNullOrWhiteSpace(model.FieldOfStudy))
            ModelState.AddModelError(nameof(model.FieldOfStudy), "Keep the existing field of study or enter a replacement. Clearing it is not available.");
        if (!string.IsNullOrWhiteSpace(p.AreaOfExpertise) && string.IsNullOrWhiteSpace(model.AreaOfExpertise))
            ModelState.AddModelError(nameof(model.AreaOfExpertise), "Keep the existing area of expertise or enter a replacement. Clearing it is not available.");
        if (qualificationChanged && uploads.Count == 0)
            ModelState.AddModelError(nameof(model.Certificates), "Upload a new certificate when changing your highest qualification.");
        if (!qualificationChanged && !fieldChanged && !expertiseChanged && uploads.Count == 0)
            ModelState.AddModelError("", "Change at least one detail or upload a new certificate before submitting.");
        if(!ModelState.IsValid) return View("Create",await Populate(model,p.TutorProfileId,token));
        var files = new List<SecureSmartTuitionSystem.Services.Learning.MaterialFile>();
        foreach(var upload in uploads) {
            var (document,error)=await TutorQualificationService.ReadDocumentAsync(upload,token);
            if(error!=null) ModelState.AddModelError(nameof(model.Certificates),error);
            else if(document!=null) files.Add(document);
        }
        if(!ModelState.IsValid) return View("Create",await Populate(model,p.TutorProfileId,token));
        var file=files.FirstOrDefault();
        db.TutorQualificationUpdateRequests.Add(new() {
            TutorProfileId=p.TutorProfileId, Qualification=QualificationChangeRules.Normalize(model.Qualification), FieldOfStudy=QualificationChangeRules.Normalize(model.FieldOfStudy),
            AreaOfExpertise=QualificationChangeRules.Normalize(model.AreaOfExpertise), Reason=model.Reason.Trim(),
            OriginalQualification=p.Qualification, OriginalFieldOfStudy=p.FieldOfStudy,
            OriginalAreaOfExpertise=p.AreaOfExpertise, OriginalVerifiedAtUtc=p.QualificationVerifiedAtUtc,
            CertificateName=file?.Name ?? "", CertificateContent=file?.Content ?? [], SubmittedAtUtc=DateTime.UtcNow,
            AdditionalFiles=files.Skip(1).Select(f=>new QualificationRequestAttachment { FileName=f.Name, Content=f.Content }).ToList() });
        TutorQualificationService.AddAudit(db,audit,HttpContext,p.ApplicationUserId,"Tutor submitted a qualification update for review.");
        try { await db.SaveChangesAsync(token); }
        catch(DbUpdateException) {
            db.ChangeTracker.Clear(); ModelState.AddModelError("","The request could not be saved. Reload to check for an existing request and try again.");
            return View("Create",await Populate(model,p.TutorProfileId,token));
        }
        TempData["QualificationSuccess"]="Qualification update submitted. Your approved details remain active until approval.";
        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles="Tutor"), HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken token)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,token);
        var r=await db.TutorQualificationUpdateRequests.Include(r=>r.TutorProfile)
            .SingleOrDefaultAsync(r=>r.Id==id && r.TutorProfile.ApplicationUserId==users.GetUserId(User),token);
        if(r==null) return NotFound();
        if(r.Status==QualificationUpdateStatus.Pending) {
            r.Status=QualificationUpdateStatus.Cancelled; r.ReviewedAtUtc=DateTime.UtcNow;
            TutorQualificationService.AddAudit(db,audit,HttpContext,r.TutorProfile.ApplicationUserId,$"Tutor cancelled qualification request #{id}.");
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }
        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles="Admin"), HttpGet]
    public async Task<IActionResult> Index(CancellationToken token, string? search=null, QualificationUpdateStatus? status=null, DateTime? from=null, DateTime? to=null) {
        SetFilters(search,status,from,to);
        return View(await Rows(Filter(db.TutorQualificationUpdateRequests.AsNoTracking(),search,status,from,to))
            .OrderBy(r=>r.Status==QualificationUpdateStatus.Pending?0:1).ThenByDescending(r=>r.Id).Take(200).ToListAsync(token));
    }

    private void SetFilters(string? search, QualificationUpdateStatus? status, DateTime? from, DateTime? to) {
        ViewData["QualificationSearch"]=search?.Trim(); ViewData["QualificationStatus"]=status;
        ViewData["QualificationFrom"]=from?.ToString("yyyy-MM-dd"); ViewData["QualificationTo"]=to?.ToString("yyyy-MM-dd");
    }
    private static IQueryable<TutorQualificationUpdateRequest> Filter(IQueryable<TutorQualificationUpdateRequest> query, string? search, QualificationUpdateStatus? status, DateTime? from, DateTime? to) {
        if(!string.IsNullOrWhiteSpace(search)) {
            var term=search.Trim();var numeric=term.TrimStart('#');int.TryParse(numeric,out var id);
            query=query.Where(r=>r.Id==id || r.TutorProfile.FullName.Contains(term) || (r.TutorProfile.ApplicationUser.UserName!=null && r.TutorProfile.ApplicationUser.UserName.Contains(term)) || r.Qualification.Contains(term) || r.FieldOfStudy.Contains(term) || r.AreaOfExpertise.Contains(term));
        }
        if(status.HasValue) query=query.Where(r=>r.Status==status.Value);
        if(from.HasValue) {var start=from.Value.Date.AddHours(-8);query=query.Where(r=>r.SubmittedAtUtc>=start);}
        if(to.HasValue) {var end=to.Value.Date.AddDays(1).AddHours(-8);query=query.Where(r=>r.SubmittedAtUtc<end);}
        return query;
    }

    [HttpGet]
    public async Task<IActionResult> Live(int? requestId, CancellationToken token, string? search=null, QualificationUpdateStatus? status=null, DateTime? from=null, DateTime? to=null)
    {
        var userId = users.GetUserId(User);
        if (userId == null) return Challenge();
        bool admin = User.IsInRole("Admin");
        var query = db.TutorQualificationUpdateRequests.AsNoTracking();
        TutorProfile? profile = null;
        if (!admin)
        {
            profile = await Own().AsNoTracking().SingleOrDefaultAsync(token);
            if (profile == null) return Forbid();
            query = query.Where(r => r.TutorProfileId == profile.TutorProfileId);
        }
        if (requestId.HasValue)
        {
            query = query.Where(r => r.Id == requestId.Value);
            if (admin) profile = await query.Select(r => r.TutorProfile).AsNoTracking().SingleOrDefaultAsync(token);
        }
        var rows = await Rows(Filter(query,search,status,from,to)).OrderBy(r => admin && r.Status == QualificationUpdateStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.Id).Take(admin ? 200 : 100).ToListAsync(token);
        QualificationLiveProfile? current = null;
        if (profile != null)
        {
            current = new() { Qualification = profile.Qualification, FieldOfStudy = profile.FieldOfStudy,
                AreaOfExpertise = profile.AreaOfExpertise, Verified = profile.QualificationVerifiedAtUtc.HasValue,
                Pending = await Rows(db.TutorQualificationUpdateRequests.AsNoTracking().Where(r=>r.TutorProfileId==profile.TutorProfileId && r.Status==QualificationUpdateStatus.Pending)).FirstOrDefaultAsync(token) };
            if (!admin) current.Documents = await db.TutorQualificationDocuments.AsNoTracking()
                .Where(d => d.TutorProfileId == profile.TutorProfileId && d.TeachingChangeRequestId == null
                    && (d.Kind == TutorDocumentKind.Certificate || d.Kind == TutorDocumentKind.Resume))
                .Select(d => new TutorDocumentSummary(d.Id, d.Kind, d.FileName)).ToListAsync(token);
        }
        return PartialView("_LiveState", new QualificationLiveState { Requests = rows, Profile = current });
    }

    private async Task<bool> PopulateDecision(QualificationDecisionInput model, CancellationToken token)
    {
        model.Request=await Rows(db.TutorQualificationUpdateRequests.AsNoTracking().Where(r=>r.Id==model.Id)).SingleOrDefaultAsync(token);
        var p=await db.TutorQualificationUpdateRequests.Where(r=>r.Id==model.Id).Select(r=>r.TutorProfile).AsNoTracking().SingleOrDefaultAsync(token);
        if(p==null) return false;
        model.CurrentCertificate = await db.TutorQualificationDocuments.AsNoTracking()
            .Where(d => d.TutorProfileId == p.TutorProfileId && d.Kind == TutorDocumentKind.Certificate && d.TeachingChangeRequestId == null)
            .Select(d => new TutorDocumentSummary(d.Id, d.Kind, d.FileName)).SingleOrDefaultAsync(token);
        model.CurrentQualification=p.Qualification; model.CurrentFieldOfStudy=p.FieldOfStudy ?? ""; model.CurrentAreaOfExpertise=p.AreaOfExpertise ?? "";
        return true;
    }
    [Authorize(Roles="Admin"), HttpGet]
    public async Task<IActionResult> Review(int id, CancellationToken token)
    {
        var model=new QualificationDecisionInput { Id=id };
        return await PopulateDecision(model,token)?View(model):NotFound();
    }

    [Authorize(Roles="Admin"), HttpPost]
    public async Task<IActionResult> Review(QualificationDecisionInput model, bool? approve, CancellationToken token)
    {
        if(!await PopulateDecision(model,token)) return NotFound();
        var admin=users.GetUserId(User); if(admin==null) return Challenge();
        if(approve==null) ModelState.AddModelError("","Select Approve or Reject.");
        if(approve==true && !model.QualificationVerified) ModelState.AddModelError(nameof(model.QualificationVerified),"Review the certificate and confirm verification before approval.");
        if(!ModelState.IsValid) return View(model);
        try {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,token);
            var r=await db.TutorQualificationUpdateRequests.Include(r=>r.AdditionalFiles).Include(r=>r.TutorProfile).ThenInclude(p=>p.ApplicationUser).SingleAsync(r=>r.Id==model.Id,token);
            if(r.Status!=QualificationUpdateStatus.Pending) { ModelState.AddModelError("","This request has already been reviewed or cancelled."); return View(model); }
            var p=r.TutorProfile;
            if(approve==true) {
                if(!p.IsActive || !p.ApplicationUser.IsActive) ModelState.AddModelError("","This tutor is inactive. Reject this request or restore the account first.");
                bool qualificationChanged = !QualificationChangeRules.Same(r.OriginalQualification, r.Qualification);
                bool fieldChanged = !QualificationChangeRules.Same(r.OriginalFieldOfStudy, r.FieldOfStudy);
                bool expertiseChanged = !QualificationChangeRules.Same(r.OriginalAreaOfExpertise, r.AreaOfExpertise);
                bool newCertificate = r.CertificateContent.Length > 0 && !string.IsNullOrWhiteSpace(r.CertificateName);
                if (qualificationChanged && !newCertificate)
                    ModelState.AddModelError("", "A highest qualification change requires a new certificate.");
                if (!qualificationChanged && !fieldChanged && !expertiseChanged && !newCertificate)
                    ModelState.AddModelError("", "This request has no changes to approve.");
                bool stale = (qualificationChanged && !QualificationChangeRules.Same(p.Qualification, r.OriginalQualification))
                    || (fieldChanged && !QualificationChangeRules.Same(p.FieldOfStudy, r.OriginalFieldOfStudy))
                    || (expertiseChanged && !QualificationChangeRules.Same(p.AreaOfExpertise, r.OriginalAreaOfExpertise))
                    || ((qualificationChanged || fieldChanged || newCertificate) && !QualificationChangeRules.Same(p.Qualification, r.OriginalQualification))
                    || (newCertificate && (!QualificationChangeRules.Same(p.FieldOfStudy, r.OriginalFieldOfStudy) || p.QualificationVerifiedAtUtc != r.OriginalVerifiedAtUtc));
                if (stale) ModelState.AddModelError("", "The affected qualification details changed after submission. Reject this outdated request and ask for a fresh submission.");
                if(!ModelState.IsValid) return View(model);
                if (qualificationChanged) p.Qualification = r.Qualification;
                if (fieldChanged) p.FieldOfStudy = r.FieldOfStudy;
                if (expertiseChanged) p.AreaOfExpertise = r.AreaOfExpertise;
                p.UpdatedAt=DateTime.UtcNow;
                if (newCertificate)
                {
                    p.QualificationVerifiedAtUtc=DateTime.UtcNow; p.QualificationVerifiedByUserId=admin;
                    var certificate=await db.TutorQualificationDocuments.SingleOrDefaultAsync(d=>d.TutorProfileId==p.TutorProfileId && d.Kind==TutorDocumentKind.Certificate && d.TeachingChangeRequestId==null,token);
                    if(certificate==null) { certificate=new() { TutorProfileId=p.TutorProfileId, Kind=TutorDocumentKind.Certificate }; db.TutorQualificationDocuments.Add(certificate); }
                    if(certificate.Id != 0) {
                        var previous=await db.QualificationRequestAttachments.Where(f=>f.CurrentDocumentId==certificate.Id).ToListAsync(token);
                        foreach(var extra in previous) extra.CurrentDocumentId=null;
                    }
                    foreach(var extra in r.AdditionalFiles) extra.CurrentDocument=certificate;
                    certificate.FileName=r.CertificateName; certificate.Content=r.CertificateContent.ToArray();
                }
            }
            r.Status=approve==true?QualificationUpdateStatus.Approved:QualificationUpdateStatus.Rejected;
            r.ReviewNotes=model.ReviewNotes.Trim(); r.ReviewedAtUtc=DateTime.UtcNow; r.ReviewedByUserId=admin;
            TutorQualificationService.AddAudit(db,audit,HttpContext,p.ApplicationUserId,$"Qualification request #{r.Id}: {r.Status}. Teaching approvals unchanged.");
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        } catch(Exception ex) when(ex is DbUpdateException or System.Data.Common.DbException) {
            db.ChangeTracker.Clear(); ModelState.AddModelError("","Unable to save the decision. Reload and try again."); return View(model);
        }
        TempData["QualificationSuccess"]="Qualification decision saved. Approved subjects and levels were not changed.";
        return RedirectToAction(nameof(Review),new { id=model.Id });
    }

    private IDataProtector BaselineProtector => HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("EduSmart.QualificationBaseline.v1");
    private sealed record QualificationBaseline(string UserId, string Qualification, string? FieldOfStudy,
        string? AreaOfExpertise, DateTime? VerifiedAtUtc, DateTime IssuedAtUtc);
    private bool BaselineMatches(string? token, TutorProfile current)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var snapshot = JsonSerializer.Deserialize<QualificationBaseline>(BaselineProtector.Unprotect(token));
            return snapshot != null && snapshot.UserId == current.ApplicationUserId
                && snapshot.IssuedAtUtc <= DateTime.UtcNow && snapshot.IssuedAtUtc > DateTime.UtcNow.AddHours(-24)
                && QualificationChangeRules.Same(snapshot.Qualification, current.Qualification)
                && QualificationChangeRules.Same(snapshot.FieldOfStudy, current.FieldOfStudy)
                && QualificationChangeRules.Same(snapshot.AreaOfExpertise, current.AreaOfExpertise)
                && snapshot.VerifiedAtUtc == current.QualificationVerifiedAtUtc;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return false; }
    }

    [Authorize(Roles="Tutor"), HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken token)
    {
        var model=new QualificationDecisionInput { Id=id };
        if(!await db.TutorQualificationUpdateRequests.AnyAsync(r=>r.Id==id && r.TutorProfile.ApplicationUserId==users.GetUserId(User) && r.TutorProfile.IsActive && r.TutorProfile.ApplicationUser.IsActive,token)) return NotFound();
        return await PopulateDecision(model,token) ? View(model) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id, CancellationToken token, int? attachmentId=null, bool preview=false)
    {
        var userId=users.GetUserId(User); if(userId==null) return Challenge();
        var request=await db.TutorQualificationUpdateRequests.AsNoTracking()
            .Where(r=>r.Id==id && (User.IsInRole("Admin") || (r.TutorProfile.ApplicationUserId==userId && r.TutorProfile.IsActive && r.TutorProfile.ApplicationUser.IsActive)))
            .Select(r=>new { r.CertificateName,r.CertificateContent }).SingleOrDefaultAsync(token);
        if(request==null) return NotFound();
        if(attachmentId is > 0) {
            var extra=await db.QualificationRequestAttachments.AsNoTracking().Where(f=>f.RequestId==id && f.Id==attachmentId).Select(f=>new {f.FileName,f.Content}).SingleOrDefaultAsync(token);
            if(extra==null) return NotFound();
            return SendDocument(extra.FileName,extra.Content,preview);
        }
        return SendDocument(request.CertificateName,request.CertificateContent,preview);
    }

    private IQueryable<TutorQualificationDocument> AccessibleCurrent(int id) => db.TutorQualificationDocuments.AsNoTracking()
        .Where(d=>d.Id==id && d.Kind==TutorDocumentKind.Certificate && d.TeachingChangeRequestId==null
            && (User.IsInRole("Admin") || (d.TutorProfile.ApplicationUserId==users.GetUserId(User) && d.TutorProfile.IsActive && d.TutorProfile.ApplicationUser.IsActive)));

    [HttpGet]
    public async Task<IActionResult> Current(int id, CancellationToken token)
    {
        var primary=await AccessibleCurrent(id).Select(d=>new QualificationFileSummary(0,d.FileName,d.Content.LongLength)).SingleOrDefaultAsync(token);
        if(primary==null) return NotFound();
        var files=await db.QualificationRequestAttachments.AsNoTracking().Where(f=>f.CurrentDocumentId==id).OrderBy(f=>f.Id).Select(f=>new QualificationFileSummary(f.Id,f.FileName,f.Content.LongLength)).ToListAsync(token);
        files.Insert(0,primary);
        return View(new QualificationFileList { OwnerId=id, Current=true, Files=files });
    }

    [HttpGet]
    public async Task<IActionResult> CurrentDocument(int id, CancellationToken token, int? attachmentId=null, bool preview=false)
    {
        if(!await AccessibleCurrent(id).AnyAsync(token)) return NotFound();
        if(attachmentId is > 0) {
            var extra=await db.QualificationRequestAttachments.AsNoTracking().Where(f=>f.Id==attachmentId && f.CurrentDocumentId==id).Select(f=>new {f.FileName,f.Content}).SingleOrDefaultAsync(token);
            return extra==null ? NotFound() : SendDocument(extra.FileName,extra.Content,preview);
        }
        var file=await AccessibleCurrent(id).Select(d=>new {d.FileName,d.Content}).SingleAsync(token);
        return SendDocument(file.FileName,file.Content,preview);
    }

    private IActionResult SendDocument(string name, byte[] content, bool preview)
    {
        if(content.Length==0) return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";
        var type=SecureSmartTuitionSystem.Services.Learning.MaterialFileService.PreviewContentType(name);
        if(preview && type!=null) {
            Response.Headers["Content-Security-Policy"]="sandbox; default-src 'none'; style-src 'unsafe-inline'";
            Response.Headers["Content-Disposition"]="inline";
            return File(content,type,enableRangeProcessing:true);
        }
        return File(content,"application/octet-stream",name);
    }
}
