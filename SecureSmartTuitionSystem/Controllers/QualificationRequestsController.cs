using System.Data;
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
            CertificateName=r.CertificateName, Reason=r.Reason, Status=r.Status,
            SubmittedAtUtc=r.SubmittedAtUtc, ReviewNotes=r.ReviewNotes });
    private async Task<QualificationUpdateInput> Populate(QualificationUpdateInput model, int tutorId, CancellationToken token)
    {
        var current = await db.TutorProfiles.AsNoTracking().SingleAsync(p => p.TutorProfileId == tutorId, token);
        model.CurrentQualification = current.Qualification;
        model.CurrentFieldOfStudy = current.FieldOfStudy;
        model.CurrentAreaOfExpertise = current.AreaOfExpertise;
        model.CurrentVerified = current.QualificationVerifiedAtUtc.HasValue;
        model.Requests = await Rows(db.TutorQualificationUpdateRequests.AsNoTracking().Where(r=>r.TutorProfileId==tutorId))
            .OrderByDescending(r=>r.Id).Take(100).ToListAsync(token);
        return model;
    }

    [Authorize(Roles="Tutor"), HttpGet]
    public async Task<IActionResult> Mine(CancellationToken token)
    {
        var p=await Own().AsNoTracking().SingleOrDefaultAsync(token);
        if(p==null) return NotFound();
        return View(await Populate(new() { Qualification=p.Qualification, FieldOfStudy=p.FieldOfStudy ?? "",
            AreaOfExpertise=p.AreaOfExpertise ?? "" },p.TutorProfileId,token));
    }

    [Authorize(Roles="Tutor"), HttpPost, RequestSizeLimit(12*1024*1024)]
    [RequestFormLimits(MultipartBodyLengthLimit=12*1024*1024)]
    public async Task<IActionResult> Mine(QualificationUpdateInput model, CancellationToken token)
    {
        var p=await Own().SingleOrDefaultAsync(token);
        if(p==null) return NotFound();
        if(await db.TutorQualificationUpdateRequests.AnyAsync(r=>r.TutorProfileId==p.TutorProfileId && r.Status==QualificationUpdateStatus.Pending,token))
            ModelState.AddModelError("", "You already have a pending qualification request. Cancel it before submitting another.");
        if(!ModelState.IsValid) return View(await Populate(model,p.TutorProfileId,token));
        var (file,error)=await TutorQualificationService.ReadDocumentAsync(model.Certificate,token);
        if(error!=null || file==null) ModelState.AddModelError(nameof(model.Certificate),error ?? "Select a certificate.");
        if(!ModelState.IsValid) return View(await Populate(model,p.TutorProfileId,token));
        db.TutorQualificationUpdateRequests.Add(new() {
            TutorProfileId=p.TutorProfileId, Qualification=model.Qualification.Trim(), FieldOfStudy=model.FieldOfStudy.Trim(),
            AreaOfExpertise=model.AreaOfExpertise.Trim(), Reason=model.Reason.Trim(),
            OriginalQualification=p.Qualification, OriginalFieldOfStudy=p.FieldOfStudy,
            OriginalAreaOfExpertise=p.AreaOfExpertise, OriginalVerifiedAtUtc=p.QualificationVerifiedAtUtc,
            CertificateName=file!.Name, CertificateContent=file.Content, SubmittedAtUtc=DateTime.UtcNow });
        TutorQualificationService.AddAudit(db,audit,HttpContext,p.ApplicationUserId,"Tutor submitted a qualification update for review.");
        try { await db.SaveChangesAsync(token); }
        catch(DbUpdateException) {
            db.ChangeTracker.Clear(); ModelState.AddModelError("","The request could not be saved. Reload to check for an existing request and try again.");
            return View(await Populate(model,p.TutorProfileId,token));
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
    public async Task<IActionResult> Index(CancellationToken token) => View(await Rows(db.TutorQualificationUpdateRequests.AsNoTracking())
        .OrderBy(r=>r.Status==QualificationUpdateStatus.Pending?0:1).ThenByDescending(r=>r.Id).Take(200).ToListAsync(token));

    private async Task<bool> PopulateDecision(QualificationDecisionInput model, CancellationToken token)
    {
        model.Request=await Rows(db.TutorQualificationUpdateRequests.AsNoTracking().Where(r=>r.Id==model.Id)).SingleOrDefaultAsync(token);
        var p=await db.TutorQualificationUpdateRequests.Where(r=>r.Id==model.Id).Select(r=>r.TutorProfile).AsNoTracking().SingleOrDefaultAsync(token);
        if(p==null) return false;
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
            var r=await db.TutorQualificationUpdateRequests.Include(r=>r.TutorProfile).ThenInclude(p=>p.ApplicationUser).SingleAsync(r=>r.Id==model.Id,token);
            if(r.Status!=QualificationUpdateStatus.Pending) { ModelState.AddModelError("","This request has already been reviewed or cancelled."); return View(model); }
            var p=r.TutorProfile;
            if(approve==true) {
                if(!p.IsActive || !p.ApplicationUser.IsActive) ModelState.AddModelError("","This tutor is inactive. Reject this request or restore the account first.");
                if(p.Qualification!=r.OriginalQualification || p.FieldOfStudy!=r.OriginalFieldOfStudy || p.AreaOfExpertise!=r.OriginalAreaOfExpertise || p.QualificationVerifiedAtUtc!=r.OriginalVerifiedAtUtc)
                    ModelState.AddModelError("","The tutor's qualifications changed after submission. Reject this outdated request and ask for a fresh submission.");
                if(!ModelState.IsValid) return View(model);
                p.Qualification=r.Qualification; p.FieldOfStudy=r.FieldOfStudy; p.AreaOfExpertise=r.AreaOfExpertise;
                p.QualificationVerifiedAtUtc=DateTime.UtcNow; p.QualificationVerifiedByUserId=admin; p.UpdatedAt=DateTime.UtcNow;
                var certificate=await db.TutorQualificationDocuments.SingleOrDefaultAsync(d=>d.TutorProfileId==p.TutorProfileId && d.Kind==TutorDocumentKind.Certificate && d.TeachingChangeRequestId==null,token);
                if(certificate==null) { certificate=new() { TutorProfileId=p.TutorProfileId, Kind=TutorDocumentKind.Certificate }; db.TutorQualificationDocuments.Add(certificate); }
                certificate.FileName=r.CertificateName; certificate.Content=r.CertificateContent.ToArray();
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

    [HttpGet]
    public async Task<IActionResult> Document(int id, CancellationToken token)
    {
        var userId=users.GetUserId(User); if(userId==null) return Challenge();
        var file=await db.TutorQualificationUpdateRequests.AsNoTracking()
            .Where(r=>r.Id==id && (User.IsInRole("Admin") || r.TutorProfile.ApplicationUserId==userId))
            .Select(r=>new { r.CertificateName,r.CertificateContent }).SingleOrDefaultAsync(token);
        if(file==null) return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(file.CertificateContent,"application/octet-stream",file.CertificateName);
    }
}
