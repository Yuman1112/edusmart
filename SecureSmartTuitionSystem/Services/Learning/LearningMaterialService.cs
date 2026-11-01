using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Learning;
namespace SecureSmartTuitionSystem.Services.Learning;

public sealed class LearningMaterialService(ApplicationDbContext db)
{
    public static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public static bool IsTutor(ClaimsPrincipal user) => user.IsInRole("Tutor") && !user.IsInRole("Admin");
    public IQueryable<ClassSession> Classes(ClaimsPrincipal user, bool manage = false)
    {
        var id = UserId(user);
        var today = ClassScheduleService.Today;
        var classes = db.ClassSessions.AsNoTracking();
        if (id.Length == 0) return classes.Where(x => false);
        if (user.IsInRole("Admin")) return manage ? classes.Where(x => false) : classes;
        if (IsTutor(user))
        {
            classes = classes.Where(x => x.TutorUserId == id && db.TutorProfiles.Any(t => t.ApplicationUserId == id
                && t.IsActive && t.ApplicationUser.IsActive && !t.ApplicationUser.MustChangePassword));
            return manage ? classes.Where(x => x.Status == ClassSessionStatus.Active && (!x.EndDate.HasValue || x.EndDate >= today)) : classes;
        }
        if (manage || !user.IsInRole("Student")) return classes.Where(x => false);
        return classes.Where(x => x.Status == ClassSessionStatus.Active && (!x.StartDate.HasValue || x.StartDate <= today)
            && (!x.EndDate.HasValue || x.EndDate >= today) && db.StudentEnrollments.Any(e => e.ClassSessionId == x.ClassSessionId
                && e.StudentProfile.ApplicationUserId == id && e.StudentProfile.IsActive
                && e.StartDate <= today && e.StartDate.Year == today.Year
                && (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop)
                && (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > today) && (!e.EndedAt.HasValue || e.EndedAt > today)));
    }
    public IQueryable<LearningMaterial> Visible(ClaimsPrincipal user)
    {
        var ids = Classes(user).Select(c => c.ClassSessionId);
        var query = db.Set<LearningMaterial>().AsNoTracking().Where(x => ids.Contains(x.ClassSessionId));
        return user.IsInRole("Admin") || IsTutor(user) ? query : query.Where(x => x.IsActive);
    }
    public async Task<List<SelectListItem>> ChoicesAsync(ClaimsPrincipal user, bool manage) =>
        await Classes(user, manage).OrderBy(x => x.ClassCode).Select(x => new SelectListItem
        { Value = x.ClassSessionId.ToString(), Text = x.ClassCode + " · " + (x.ClassName ?? "") }).ToListAsync();

    private IQueryable<MaterialRow> Rows(IQueryable<LearningMaterial> query) => query.Select(x => new MaterialRow
    {
        Id = x.LearningMaterialId,
        FileCount = 1 + db.Set<LearningMaterialAttachment>().Count(a => a.LearningMaterialId == x.LearningMaterialId),
        ClassId = x.ClassSessionId,
        Title = x.Title,
        Description = x.Description,
        ClassCode = x.ClassSession.ClassCode,
        ClassName = x.ClassSession.ClassName ?? "",
        Subject = x.ClassSession.Subject.SubjectName,
        Level = x.ClassSession.Subject.AcademicLevel,
        Tutor = x.ClassSession.TutorUser.FullName ?? "",
        FileName = x.OriginalFileName,
        Size = x.FileSize,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc,
        RowVersion = Convert.ToBase64String(x.RowVersion)
    });
    public async Task<MaterialIndexModel> IndexAsync(ClaimsPrincipal user, MaterialFilter filter)
    {
        var model = new MaterialIndexModel { Filter = filter, IsTutor = IsTutor(user), IsStaff = user.IsInRole("Admin") || IsTutor(user), Classes = await ChoicesAsync(user, false) };
        var query = Visible(user);
        if (filter.ClassId.HasValue) query = query.Where(x => x.ClassSessionId == filter.ClassId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Title.Contains(filter.Search.Trim()));
        if (model.IsStaff && filter.Status != "all") query = query.Where(x => x.IsActive == (filter.Status == "active"));
        model.Total = await query.CountAsync();
        filter.Page = Math.Min(filter.Page, model.PageCount);
        model.Rows = await Rows(query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.LearningMaterialId).Skip((filter.Page - 1) * 25).Take(25)).ToListAsync();
        if (model.IsTutor)
        {
            var manageable = (await Classes(user, true).Select(c => c.ClassSessionId).ToListAsync()).ToHashSet();
            foreach (var row in model.Rows) row.CanManage = manageable.Contains(row.ClassId);
        }
        return model;
    }
    public async Task<MaterialRow?> DetailsAsync(ClaimsPrincipal user, int id)
    {
        var row = await Rows(Visible(user).Where(x => x.LearningMaterialId == id)).SingleOrDefaultAsync();
        if (row != null) row.CanManage = await Classes(user, true).AnyAsync(x => x.ClassSessionId == row.ClassId);
        if (row != null)
        {
            row.Attachments.Add(new MaterialAttachmentRow { Id = 0, Name = row.FileName, Size = row.Size });
            row.Attachments.AddRange(await db.Set<LearningMaterialAttachment>().AsNoTracking().Where(a => a.LearningMaterialId == id).OrderBy(a => a.LearningMaterialAttachmentId)
                .Select(a => new MaterialAttachmentRow { Id = a.LearningMaterialAttachmentId, Name = a.OriginalFileName, Size = a.FileSize }).ToListAsync());
        }
        return row;
    }
    public async Task<MaterialForm?> EditAsync(ClaimsPrincipal user, int id)
    {
        var ids = Classes(user, true).Select(c => c.ClassSessionId);
        var model = await db.Set<LearningMaterial>().AsNoTracking().Where(x => x.LearningMaterialId == id && ids.Contains(x.ClassSessionId))
            .Select(x => new MaterialForm
            {
                Id = x.LearningMaterialId,
                ClassSessionId = x.ClassSessionId,
                Title = x.Title,
                Description = x.Description,
                CurrentFileName = x.OriginalFileName,
                RowVersion = Convert.ToBase64String(x.RowVersion)
            }).SingleOrDefaultAsync();
        if (model != null)
        {
            var details = await DetailsAsync(user, id);
            model.CurrentFiles = details?.Attachments ?? [];
        }
        return model;
    }
    public Task<string?> SaveAsync(ClaimsPrincipal user, MaterialForm form, MaterialFile? file) => SaveFilesAsync(user, form, file == null ? [] : [file]);
    public async Task<string?> SaveFilesAsync(ClaimsPrincipal user, MaterialForm form, IReadOnlyList<MaterialFile> files)
    {
        if (files.Count > 10 || files.Sum(f => (long)f.Content.Length) > 31457280 || files.Any(f => f.Content.Length == 0 || f.Content.Length > MaterialFileService.MaxBytes))
            return "Choose up to 10 files, each at most 10 MB and 30 MB in total.";
        var file = files.FirstOrDefault();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (!IsTutor(user) || !await Classes(user, true).AnyAsync(x => x.ClassSessionId == form.ClassSessionId))
                return "This class is no longer available for managing materials. Reload and check your assignment.";
            LearningMaterial material;
            if (form.Id == 0)
            {
                if (file == null) return "Choose a material file.";
                material = new LearningMaterial { ClassSessionId = form.ClassSessionId!.Value, UploadedByUserId = UserId(user) };
                db.Set<LearningMaterial>().Add(material);
            }
            else
            {
                var found = await db.Set<LearningMaterial>().SingleOrDefaultAsync(x => x.LearningMaterialId == form.Id && x.ClassSessionId == form.ClassSessionId);
                if (found == null || !string.Equals(Convert.ToBase64String(found.RowVersion), form.RowVersion, StringComparison.Ordinal))
                    return "This material changed or is unavailable. Reload before editing again.";
                material = found;
            }
            var removed = form.RemovedAttachmentIds.ToHashSet();
            var extras = form.Id == 0 ? [] : await db.Set<LearningMaterialAttachment>().AsNoTracking()
                .Where(a => a.LearningMaterialId == form.Id)
                .Select(a => new { a.LearningMaterialAttachmentId, a.FileSize }).ToListAsync();
            if (removed.Any(id => form.Id == 0 || (id != 0 && !extras.Any(a => a.LearningMaterialAttachmentId == id))))
                return "An attachment is unavailable or belongs to another material. Reload before editing.";
            var retained = extras.Where(a => !removed.Contains(a.LearningMaterialAttachmentId)).ToList();
            var count = (form.Id != 0 && !removed.Contains(0) ? 1 : 0) + retained.Count + files.Count;
            var size = (form.Id != 0 && !removed.Contains(0) ? (long)material.FileSize : 0) + retained.Sum(a => (long)a.FileSize) + files.Sum(f => (long)f.Content.Length);
            if (count < 1 || count > 10 || size > 31457280)
                return "Keep at least one attachment, at most 10 files and 30 MB in total.";
            var additions = files.ToList();
            if (form.Id == 0 || removed.Contains(0))
            {
                MaterialFile primary;
                if (retained.Count > 0)
                {
                    var promoteId = retained[0].LearningMaterialAttachmentId;
                    var promoted = await db.Set<LearningMaterialAttachment>().SingleAsync(a => a.LearningMaterialAttachmentId == promoteId && a.LearningMaterialId == form.Id);
                    primary = new MaterialFile(promoted.OriginalFileName, promoted.ContentType, promoted.FileContent);
                    db.Remove(promoted);
                }
                else { primary = additions[0]; additions.RemoveAt(0); }
                material.OriginalFileName = primary.Name; material.ContentType = primary.ContentType;
                material.FileContent = primary.Content; material.FileSize = primary.Content.Length;
            }
            if (form.Id != 0 && removed.Any(id => id != 0))
                db.RemoveRange(await db.Set<LearningMaterialAttachment>().Where(a => a.LearningMaterialId == form.Id && removed.Contains(a.LearningMaterialAttachmentId)).ToListAsync());
            foreach (var attachment in additions)
                db.Add(new LearningMaterialAttachment { LearningMaterial = material, OriginalFileName = attachment.Name, ContentType = attachment.ContentType, FileSize = attachment.Content.Length, FileContent = attachment.Content });
            material.Title = form.Title.Trim(); material.Description = form.Description?.Trim();
            material.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(); await tx.CommitAsync(); form.Id = material.LearningMaterialId; return null;
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "This material changed. Reload before saving again."; }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqlException)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "The material could not be saved. Reload and try again. Re-select the file if you were uploading one."; }
    }
    public async Task<string?> StatusAsync(ClaimsPrincipal user, int id, string? version, bool active)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var ids = Classes(user, true).Select(c => c.ClassSessionId);
            var material = await db.Set<LearningMaterial>().SingleOrDefaultAsync(x => x.LearningMaterialId == id && ids.Contains(x.ClassSessionId));
            if (material == null) return "This material is not available for updating.";
            if (!string.Equals(Convert.ToBase64String(material.RowVersion), version, StringComparison.Ordinal)) return "This material changed. Reload before changing its status.";
            material.IsActive = active; material.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "This material changed. Reload and try again."; }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqlException)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "The status could not be saved. Reload and try again."; }
    }
}
