using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Learning;
namespace SecureSmartTuitionSystem.Services.Learning;

public static class AssessmentRules
{
    public static DateTime NowMyt => DateTime.UtcNow.AddHours(8);
    public static bool CanSubmit(Assessment a, DateTime now) => a.IsActive && a.PublishedAtUtc.HasValue && !a.ResultsPublishedAtUtc.HasValue && now >= a.OpensAtMyt && now < a.DueAtMyt;
    public static bool CanEdit(Assessment a, DateTime now) => a.IsActive && !a.ResultsPublishedAtUtc.HasValue && (!a.PublishedAtUtc.HasValue || now < a.OpensAtMyt);
    public static bool ValidMarks(decimal marks, decimal maximum) => maximum > 0 && marks >= 0 && marks <= maximum && decimal.Round(marks, 2) == marks;
    public static string Grade(decimal percentage) => percentage >= 80 ? "A" : percentage >= 70 ? "B" : percentage >= 60 ? "C" : percentage >= 50 ? "D" : "F";
    public static bool Version(byte[] current, string? supplied) => !string.IsNullOrEmpty(supplied) && string.Equals(Convert.ToBase64String(current), supplied, StringComparison.Ordinal);
}
public sealed class AssessmentService(ApplicationDbContext db)
{
    public static bool Tutor(ClaimsPrincipal user) => LearningMaterialService.IsTutor(user);
    public static bool Staff(ClaimsPrincipal user) => user.IsInRole("Admin") || Tutor(user);
    private static string Own(ClaimsPrincipal user) => LearningMaterialService.UserId(user);
    public IQueryable<Assessment> Visible(ClaimsPrincipal user)
    {
        var query = db.Set<Assessment>().AsNoTracking();
        var classes = new LearningMaterialService(db).Classes(user).Select(x => x.ClassSessionId);
        if (Staff(user)) return query.Where(x => classes.Contains(x.ClassSessionId));
        if (!user.IsInRole("Student")) return query.Where(x => false);
        var id = Own(user); var now = AssessmentRules.NowMyt;
        return query.Where(x => classes.Contains(x.ClassSessionId) && x.IsActive && x.PublishedAtUtc != null && x.OpensAtMyt <= now
            && x.Participants.Any(p => p.StudentProfile.ApplicationUserId == id && p.StudentProfile.IsActive));
    }
    private IQueryable<Assessment> Manage(ClaimsPrincipal user)
    {
        var classes = new LearningMaterialService(db).Classes(user, true).Select(x => x.ClassSessionId);
        return db.Set<Assessment>().Where(x => Tutor(user) && classes.Contains(x.ClassSessionId));
    }
    public IQueryable<AssessmentParticipant> ResultScope(ClaimsPrincipal user)
    {
        var q = db.Set<AssessmentParticipant>().AsNoTracking();
        if (Staff(user)) { var ids = Visible(user).Select(x => x.AssessmentId); return q.Where(x => ids.Contains(x.AssessmentId)); }
        if (!user.IsInRole("Student") && !user.IsInRole("Parent")) return q.Where(x => false);
        var id = Own(user); if (id.Length == 0) return q.Where(x => false); bool parent = user.IsInRole("Parent") && !user.IsInRole("Student");
        var today = ClassScheduleService.Today; var now = AssessmentRules.NowMyt;
        return q.Where(p => p.Assessment.IsActive && p.Assessment.PublishedAtUtc != null && p.Assessment.OpensAtMyt <= now
            && p.StudentProfile.IsActive && (parent ? p.StudentProfile.ParentStudents.Any(l => l.IsActive && l.ParentProfile.ApplicationUserId == id) : p.StudentProfile.ApplicationUserId == id)
            && db.StudentEnrollments.Any(e => e.StudentProfileId == p.StudentProfileId && e.ClassSessionId == p.Assessment.ClassSessionId
                && e.StartDate <= today && e.StartDate.Year == today.Year && (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop)
                && (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > today) && (!e.EndedAt.HasValue || e.EndedAt > today)
                && e.ClassSession.Status == ClassSessionStatus.Active && (!e.ClassSession.StartDate.HasValue || e.ClassSession.StartDate <= today)
                && (!e.ClassSession.EndDate.HasValue || e.ClassSession.EndDate >= today)));
    }
    private IQueryable<AssessmentRow> Rows(IQueryable<Assessment> q) => q.Select(x => new AssessmentRow
    {
        Id = x.AssessmentId,
        ClassId = x.ClassSessionId,
        Title = x.Title,
        Kind = x.Kind,
        Instructions = x.Instructions,
        MaxMarks = x.MaxMarks,
        ClassCode = x.ClassSession.ClassCode,
        ClassName = x.ClassSession.ClassName ?? "",
        Subject = x.ClassSession.Subject.SubjectName,
        Tutor = x.ClassSession.TutorUser.FullName ?? "",
        OpensAtMyt = x.OpensAtMyt,
        DueAtMyt = x.DueAtMyt,
        IsActive = x.IsActive,
        PublishedAtUtc = x.PublishedAtUtc,
        ResultsPublishedAtUtc = x.ResultsPublishedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc,
        RowVersion = Convert.ToBase64String(x.RowVersion),
        Participants = x.Participants.Count,
        Submitted = x.Participants.Count(p => p.SubmittedAtUtc != null),
        Graded = x.Participants.Count(p => p.Marks != null)
    });
    private static IQueryable<Assessment> Order(IQueryable<Assessment> q, string sort) => sort switch
    {
        "due_desc" => q.OrderByDescending(x => x.DueAtMyt).ThenByDescending(x => x.AssessmentId),
        "title_asc" => q.OrderBy(x => x.Title).ThenBy(x => x.AssessmentId),
        "title_desc" => q.OrderByDescending(x => x.Title).ThenBy(x => x.AssessmentId),
        "class_asc" => q.OrderBy(x => x.ClassSession.ClassCode).ThenBy(x => x.ClassSession.TutorUser.FullName).ThenBy(x => x.AssessmentId),
        "class_desc" => q.OrderByDescending(x => x.ClassSession.ClassCode).ThenByDescending(x => x.ClassSession.TutorUser.FullName).ThenBy(x => x.AssessmentId),
        "status_asc" => q.OrderBy(x => !x.IsActive ? (x.PublishedAtUtc == null ? 4 : 3) : x.PublishedAtUtc == null ? 0 : x.ResultsPublishedAtUtc == null ? 1 : 2).ThenBy(x => x.DueAtMyt).ThenBy(x => x.AssessmentId),
        "status_desc" => q.OrderByDescending(x => !x.IsActive ? (x.PublishedAtUtc == null ? 4 : 3) : x.PublishedAtUtc == null ? 0 : x.ResultsPublishedAtUtc == null ? 1 : 2).ThenBy(x => x.DueAtMyt).ThenBy(x => x.AssessmentId),
        "updated_desc" => q.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.AssessmentId),
        _ => q.OrderBy(x => x.DueAtMyt).ThenBy(x => x.AssessmentId)
    };
    public async Task<AssessmentIndexModel> IndexAsync(ClaimsPrincipal user, AssessmentFilter filter)
    {
        var model = new AssessmentIndexModel { Filter = filter, IsStaff = Staff(user), IsTutor = Tutor(user), Classes = await new LearningMaterialService(db).ChoicesAsync(user, false) };
        var q = Visible(user);
        model.Subjects = await q.Select(x => new { x.ClassSession.SubjectId, x.ClassSession.Subject.SubjectName }).Distinct().OrderBy(x => x.SubjectName).Select(x => new SelectListItem { Value = x.SubjectId.ToString(), Text = x.SubjectName }).ToListAsync();
        if (filter.SubjectId.HasValue) q = q.Where(x => x.ClassSession.SubjectId == filter.SubjectId);
        if (filter.Kind.HasValue) q = q.Where(x => x.Kind == filter.Kind);
        if (!string.IsNullOrEmpty(filter.Month) && DateTime.TryParseExact(filter.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month)) { var end = month.AddMonths(1); q = q.Where(x => x.DueAtMyt >= month && x.DueAtMyt < end); }
        if (filter.ClassId.HasValue) q = q.Where(x => x.ClassSessionId == filter.ClassId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) { var term = filter.Search.Trim(); q = q.Where(x => x.Title.Contains(term)); }
        if (model.IsStaff) q = filter.Status switch { "inactive" => q.Where(x => !x.IsActive), "draft" => q.Where(x => x.IsActive && x.PublishedAtUtc == null), "active" => q.Where(x => x.IsActive), _ => q };
        model.Total = await q.CountAsync();
        filter.Page = Math.Min(filter.Page, model.PageCount);
        model.Rows = await Rows(Order(q, filter.Sort).Skip((filter.Page - 1) * 25).Take(25)).ToListAsync(); return model;
    }
    public async Task<AssessmentDetailsModel?> DetailsAsync(ClaimsPrincipal user, int id)
    {
        var row = await Rows(Visible(user).Where(x => x.AssessmentId == id)).SingleOrDefaultAsync(); if (row == null) return null;
        var now = AssessmentRules.NowMyt; bool manage = Tutor(user) && await Manage(user).AnyAsync(x => x.AssessmentId == id);
        var model = new AssessmentDetailsModel
        {
            Assessment = row,
            IsStaff = Staff(user),
            CanManage = manage,
            CanEdit = manage && row.IsActive && !row.ResultsPublishedAtUtc.HasValue && (!row.PublishedAtUtc.HasValue || now < row.OpensAtMyt),
            CanGrade = manage && row.PublishedAtUtc.HasValue && !row.ResultsPublishedAtUtc.HasValue,
            Submission = new SubmissionForm { AssessmentId = id, RowVersion = row.RowVersion }
        };
        model.QuestionFiles = await db.Set<AssessmentFile>().AsNoTracking().Where(x => x.AssessmentId == id && x.AssessmentParticipantId == null)
            .OrderBy(x => x.AssessmentFileId).Select(x => new AssessmentFileRow { Id = x.AssessmentFileId, Name = x.Name, Size = x.FileSize }).ToListAsync();
        var people = ResultScope(user).Where(x => x.AssessmentId == id);
        // Nonstaff projections never include unreleased grade values.
        bool seeDraft = model.IsStaff;
        model.Participants = await people.OrderBy(x => x.StudentNumber).Select(p => new AssessmentParticipantRow
        {
            Id = p.AssessmentParticipantId,
            StudentId = p.StudentProfileId,
            StudentNumber = p.StudentNumber,
            StudentName = p.StudentName,
            SubmittedAtUtc = p.SubmittedAtUtc,
            SubmissionNote = p.SubmissionNote,
            Marks = seeDraft || p.Assessment.ResultsPublishedAtUtc != null ? p.Marks : null,
            Feedback = seeDraft || p.Assessment.ResultsPublishedAtUtc != null ? p.Feedback : null,
            GradedAtUtc = seeDraft || p.Assessment.ResultsPublishedAtUtc != null ? p.GradedAtUtc : null,
            RowVersion = Convert.ToBase64String(p.RowVersion)
        }).ToListAsync();
        var participantIds = model.Participants.Select(x => x.Id).ToList();
        var files = await db.Set<AssessmentFile>().AsNoTracking().Where(x => x.AssessmentId == id && x.AssessmentParticipantId != null && participantIds.Contains(x.AssessmentParticipantId.Value))
            .Select(x => new { Owner = x.AssessmentParticipantId, File = new AssessmentFileRow { Id = x.AssessmentFileId, Name = x.Name, Size = x.FileSize } }).ToListAsync();
        foreach (var p in model.Participants) p.Files = files.Where(x => x.Owner == p.Id).Select(x => x.File).ToList();
        model.CanSubmit = !model.IsStaff && user.IsInRole("Student") && row.IsActive && row.PublishedAtUtc.HasValue && !row.ResultsPublishedAtUtc.HasValue
            && now >= row.OpensAtMyt && now < row.DueAtMyt && model.Participants.Any(x => x.SubmittedAtUtc == null);
        model.CanUnsubmit = !model.IsStaff && user.IsInRole("Student") && row.IsActive && row.PublishedAtUtc.HasValue && !row.ResultsPublishedAtUtc.HasValue
            && now >= row.OpensAtMyt && now < row.DueAtMyt
            && await people.AnyAsync(p => p.SubmittedAtUtc != null && p.Marks == null && p.GradedAtUtc == null);
        if (!model.IsStaff)
        {
            var ownAnswer = model.Participants.SingleOrDefault();
            model.Submission.Note = ownAnswer?.SubmissionNote;
            model.Submission.ParticipantRowVersion = ownAnswer?.RowVersion;
            model.Submission.KeptFileIds = ownAnswer?.Files.Select(f => f.Id).ToList() ?? [];
        }
        return model;
    }
    public async Task<AssessmentForm?> FormAsync(ClaimsPrincipal user, int id)
    {
        AssessmentForm form;
        if (id == 0) form = new() { OpensAtMyt = AssessmentRules.NowMyt.AddMinutes(5), DueAtMyt = AssessmentRules.NowMyt.AddDays(7) };
        else
        {
            var a = await Manage(user).AsNoTracking().SingleOrDefaultAsync(x => x.AssessmentId == id);
            if (a == null || !AssessmentRules.CanEdit(a, AssessmentRules.NowMyt)) return null;
            form = new()
            {
                Id = id,
                ClassSessionId = a.ClassSessionId,
                Title = a.Title,
                Kind = a.Kind,
                Instructions = a.Instructions,
                MaxMarks = a.MaxMarks,
                OpensAtMyt = a.OpensAtMyt,
                DueAtMyt = a.DueAtMyt,
                RowVersion = Convert.ToBase64String(a.RowVersion)
            };
        }
        await FillFormAsync(user, form); return form;
    }
    public async Task FillFormAsync(ClaimsPrincipal user, AssessmentForm form)
    {
        form.Classes = await new LearningMaterialService(db).ChoicesAsync(user, true);
        form.ExistingFiles = await db.Set<AssessmentFile>().AsNoTracking().Where(x => x.AssessmentId == form.Id && x.AssessmentParticipantId == null)
            .OrderBy(x => x.AssessmentFileId).Select(x => new AssessmentFileRow { Id = x.AssessmentFileId, Name = x.Name, Size = x.FileSize }).ToListAsync();
    }
    public async Task<string?> SaveAsync(ClaimsPrincipal user, AssessmentForm form, IReadOnlyList<MaterialFile> files)
    {
        return await Write(async () => {
            if (!Tutor(user) || !await new LearningMaterialService(db).Classes(user, true).AnyAsync(c => c.ClassSessionId == form.ClassSessionId)) return "This class is unavailable for assessment management.";
            if (string.IsNullOrWhiteSpace(form.Title) || form.Title.Length > 150 || !Enum.IsDefined(form.Kind) || form.MaxMarks <= 0 || form.MaxMarks > 10000 || decimal.Round(form.MaxMarks, 2) != form.MaxMarks
                || form.OpensAtMyt == null || form.DueAtMyt == null || form.OpensAtMyt.Value.Year < 2000 || form.DueAtMyt.Value.Year > 2100 || form.DueAtMyt <= form.OpensAtMyt || form.DueAtMyt <= AssessmentRules.NowMyt)
                return "Check the title, marks and dates. The deadline must be in the future.";
            var cls = await db.ClassSessions.AsNoTracking().SingleAsync(c => c.ClassSessionId == form.ClassSessionId);
            if (cls.StartDate.HasValue && form.OpensAtMyt.Value.Date < cls.StartDate.Value.Date || cls.EndDate.HasValue && form.DueAtMyt.Value.Date > cls.EndDate.Value.Date) return "Opening and due dates must be within the class term.";
            Assessment a;
            if (form.Id == 0) { a = new() { ClassSessionId = cls.ClassSessionId, CreatedByUserId = Own(user) }; db.Add(a); }
            else
            {
                var found = await Manage(user).Where(x => x.AssessmentId == form.Id).AsTracking().SingleOrDefaultAsync();
                if (found == null || !AssessmentRules.Version(found.RowVersion, form.RowVersion)) return "This assessment changed. Reload before saving.";
                if (!AssessmentRules.CanEdit(found, AssessmentRules.NowMyt) || found.ClassSessionId != form.ClassSessionId) return "Restore or reactivate this assessment before editing. Published assessments can only be edited before opening and before results are released. The class cannot be changed.";
                a = found;
            }
            var old = await db.Set<AssessmentFile>().Where(x => x.AssessmentId == form.Id && x.AssessmentParticipantId == null).AsTracking().ToListAsync();
            var remove = form.RemovedFileIds.ToHashSet(); if (remove.Any(id => !old.Any(x => x.AssessmentFileId == id))) return "An attachment is invalid. Reload.";
            var retained = old.Where(x => !remove.Contains(x.AssessmentFileId)).ToList();
            if (!ValidFiles(files, retained.Count, retained.Sum(x => (long)x.FileSize))) return "Use up to 10 attachments, 10 MB each and 30 MB total.";
            db.RemoveRange(old.Where(x => remove.Contains(x.AssessmentFileId)));
            a.Title = form.Title.Trim(); a.Kind = form.Kind; a.Instructions = form.Instructions?.Trim(); a.MaxMarks = form.MaxMarks;
            a.OpensAtMyt = DateTime.SpecifyKind(form.OpensAtMyt.Value, DateTimeKind.Unspecified); a.DueAtMyt = DateTime.SpecifyKind(form.DueAtMyt.Value, DateTimeKind.Unspecified); a.UpdatedAtUtc = DateTime.UtcNow;
            foreach (var file in files) db.Add(new AssessmentFile { Assessment = a, Name = file.Name, ContentType = file.ContentType, FileSize = file.Content.Length, Content = file.Content });
            await db.SaveChangesAsync(); form.Id = a.AssessmentId; return null;
        });
    }
    public async Task<string?> ActionAsync(ClaimsPrincipal user, int id, string? version, string action)
    {
        return await Write(async () => {
            var a = await Manage(user).Where(x => x.AssessmentId == id).AsTracking().SingleOrDefaultAsync();
            if (a == null || !AssessmentRules.Version(a.RowVersion, version)) return "This assessment changed or is unavailable. Reload.";
            if (action == "publish")
            {
                if (a.PublishedAtUtc != null || !a.IsActive || a.DueAtMyt <= AssessmentRules.NowMyt) return "Only an active draft with a future deadline can be published.";
                var today = ClassScheduleService.Today;
                var students = await db.StudentEnrollments.AsNoTracking().Where(e => e.ClassSessionId == a.ClassSessionId && e.StudentProfile.IsActive && e.StudentProfile.ApplicationUserId != null
                    && e.StartDate <= today && e.StartDate.Year == today.Year && (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop)
                    && (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > today) && (!e.EndedAt.HasValue || e.EndedAt > today))
                    .Select(e => new { e.StudentProfileId, e.StudentProfile.StudentNumber, e.StudentProfile.FullName }).Distinct().Take(501).ToListAsync();
                if (students.Count == 0) return "No eligible students. Enroll students before publishing.";
                if (students.Count > 500) return "This class exceeds the assessment roster limit.";
                foreach (var s in students) a.Participants.Add(new() { StudentProfileId = s.StudentProfileId, StudentNumber = s.StudentNumber, StudentName = s.FullName });
                a.PublishedAtUtc = DateTime.UtcNow;
            }
            else if (action == "release")
            {
                if (!a.IsActive || a.PublishedAtUtc == null || a.ResultsPublishedAtUtc != null || AssessmentRules.NowMyt < a.DueAtMyt) return "Results can be released once, after the deadline.";
                if (!await db.Set<AssessmentParticipant>().AnyAsync(p => p.AssessmentId == id && p.SubmittedAtUtc != null)) return "No submissions are available to release.";
                if (await db.Set<AssessmentParticipant>().AnyAsync(p => p.AssessmentId == id && p.SubmittedAtUtc != null && p.Marks == null)) return "Grade every submitted answer before releasing results.";
                a.ResultsPublishedAtUtc = DateTime.UtcNow;
            }
            else if (action == "deactivate") a.IsActive = false;
            else if (action == "reactivate") a.IsActive = true;
            else return "Choose a valid action.";
            a.UpdatedAtUtc = DateTime.UtcNow;
            if (await db.SaveChangesAsync() == 0) return "No change was saved. Reload.";
            return null;
        });
    }
    public async Task<string?> SubmitAsync(ClaimsPrincipal user, SubmissionForm form, IReadOnlyList<MaterialFile> files)
    {
        return await Write(async () => {
            if (Staff(user) || !user.IsInRole("Student")) return "Only the enrolled student can submit.";
            var a = await Visible(user).Where(x => x.AssessmentId == form.AssessmentId).AsTracking().SingleOrDefaultAsync();
            if (a == null || !AssessmentRules.Version(a.RowVersion, form.RowVersion)) return "The assessment changed. Reload before submitting.";
            if (!AssessmentRules.CanSubmit(a, AssessmentRules.NowMyt)) return "Submissions are closed or the assessment is inactive.";
            var own = Own(user);
            var p = await db.Set<AssessmentParticipant>().Where(x => x.AssessmentId == a.AssessmentId && x.StudentProfile.ApplicationUserId == own).AsTracking().SingleOrDefaultAsync();
            if (p == null || p.SubmittedAtUtc != null) return "Your answer is already submitted or you are not on the published roster.";
            if (!AssessmentRules.Version(p.RowVersion, form.ParticipantRowVersion)) return "Your answer changed. Reload before submitting again.";
            if (p.Marks.HasValue || p.GradedAtUtc.HasValue) return "This answer has already been graded and is locked.";
            var previousFiles = await db.Set<AssessmentFile>().Where(f => f.AssessmentId == a.AssessmentId && f.AssessmentParticipantId == p.AssessmentParticipantId).AsTracking().ToListAsync();
            var keptIds = form.KeptFileIds.Distinct().ToHashSet();
            if (keptIds.Count > 10 || keptIds.Any(id => !previousFiles.Any(f => f.AssessmentFileId == id))) return "An attachment is unavailable or does not belong to your answer. Reload.";
            var kept = previousFiles.Where(f => keptIds.Contains(f.AssessmentFileId)).ToList();
            if (kept.Count + files.Count == 0 || kept.Count + files.Count > 10
                || kept.Sum(f => (long)f.FileSize) + files.Sum(f => (long)f.Content.Length) > 31457280
                || (files.Count > 0 && !ValidFiles(files)))
                return "Keep at least one answer file (up to 10 files, 10 MB each and 30 MB total).";
            if (form.Note?.Length > 2000) return "The note is too long.";
            if (!AssessmentRules.CanSubmit(a, AssessmentRules.NowMyt)) return "The deadline has passed.";
            db.RemoveRange(previousFiles.Where(f => !keptIds.Contains(f.AssessmentFileId)));
            p.SubmissionNote = form.Note?.Trim(); p.SubmittedAtUtc = DateTime.UtcNow;
            foreach (var f in files) db.Add(new AssessmentFile { AssessmentId = a.AssessmentId, Participant = p, Name = f.Name, ContentType = f.ContentType, FileSize = f.Content.Length, Content = f.Content });
            await db.SaveChangesAsync(); return null;
        });
    }
    public async Task<string?> UnsubmitAsync(ClaimsPrincipal user, int id, string? rowVersion)
    {
        return await Write(async () => {
            if (Staff(user) || !user.IsInRole("Student")) return "Only the enrolled student can withdraw an answer.";
            var a = await Visible(user).Where(x => x.AssessmentId == id).AsTracking().SingleOrDefaultAsync();
            if (a == null || !AssessmentRules.CanSubmit(a, AssessmentRules.NowMyt)) return "Withdrawal is closed or this assessment is unavailable.";
            var own = Own(user);
            var p = await db.Set<AssessmentParticipant>().Where(x => x.AssessmentId == id && x.StudentProfile.ApplicationUserId == own).AsTracking().SingleOrDefaultAsync();
            if (p == null || !AssessmentRules.Version(p.RowVersion, rowVersion)) return "Your submission changed. Reload before trying again.";
            if (p.SubmittedAtUtc == null || p.Marks.HasValue || p.GradedAtUtc.HasValue) return "Only an ungraded submitted answer can be withdrawn.";
            if (!AssessmentRules.CanSubmit(a, AssessmentRules.NowMyt)) return "The deadline has passed.";
            // Keep previous files for reference until a replacement is successfully submitted.
            p.SubmittedAtUtc = null;
            await db.SaveChangesAsync(); return null;
        });
    }
    public async Task<string?> GradeAsync(ClaimsPrincipal user, AssessmentGradeForm form)
    {
        return await Write(async () => {
            var ids = Manage(user).Select(x => x.AssessmentId);
            var p = await db.Set<AssessmentParticipant>().Where(x => x.AssessmentParticipantId == form.ParticipantId && ids.Contains(x.AssessmentId)).AsTracking().SingleOrDefaultAsync();
            if (p == null || !AssessmentRules.Version(p.RowVersion, form.RowVersion)) return "The submission changed or is unavailable. Reload.";
            var a = await Manage(user).Where(x => x.AssessmentId == p.AssessmentId).AsTracking().SingleAsync();
            if (!a.IsActive || a.PublishedAtUtc == null || a.ResultsPublishedAtUtc != null || p.SubmittedAtUtc == null) return "This answer is not open for grading. Released results remain locked.";
            if (!form.Marks.HasValue || !AssessmentRules.ValidMarks(form.Marks.Value, a.MaxMarks)) return $"Enter marks from 0 to {a.MaxMarks:0.##}, with at most two decimal places.";
            if (form.Feedback?.Length > 2000) return "Feedback must be 2,000 characters or fewer.";
            var feedback = string.IsNullOrWhiteSpace(form.Feedback) ? null : form.Feedback.Trim();
            var previousFeedback = string.IsNullOrWhiteSpace(p.Feedback) ? null : p.Feedback.Trim();
            if (p.Marks == form.Marks && string.Equals(previousFeedback, feedback, StringComparison.Ordinal))
                return "No changes to save. Update marks or feedback before saving.";
            p.Marks = form.Marks; p.Feedback = feedback; p.GradedAtUtc = DateTime.UtcNow; p.GradedByUserId = Own(user); a.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(); return null;
        });
    }
    public async Task<AssessmentProgressModel> ProgressAsync(ClaimsPrincipal user, AssessmentFilter filter)
    {
        var model = new AssessmentProgressModel { Filter = filter, IsStaff = Staff(user), IsParent = user.IsInRole("Parent") && !Staff(user) };
        var scope = ResultScope(user);
        model.SubjectChoices = await scope.Select(p => new { p.Assessment.ClassSession.SubjectId, p.Assessment.ClassSession.Subject.SubjectName }).Distinct().OrderBy(x => x.SubjectName).Select(x => new SelectListItem { Value = x.SubjectId.ToString(), Text = x.SubjectName }).ToListAsync();
        if (filter.SubjectId.HasValue) scope = scope.Where(p => p.Assessment.ClassSession.SubjectId == filter.SubjectId);
        if (filter.Kind.HasValue) scope = scope.Where(p => p.Assessment.Kind == filter.Kind);
        if (!string.IsNullOrEmpty(filter.Month) && DateTime.TryParseExact(filter.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month)) { var end = month.AddMonths(1); scope = scope.Where(p => p.Assessment.DueAtMyt >= month && p.Assessment.DueAtMyt < end); }
        model.Students = await scope.Select(p => new { p.StudentProfileId, p.StudentNumber, p.StudentName }).Distinct().OrderBy(x => x.StudentName)
            .Select(x => new SelectListItem { Value = x.StudentProfileId.ToString(), Text = x.StudentNumber + " · " + x.StudentName }).ToListAsync();
        model.Classes = await scope.Select(p => new { p.Assessment.ClassSessionId, p.Assessment.ClassSession.ClassCode }).Distinct().OrderBy(x => x.ClassCode)
            .Select(x => new SelectListItem { Value = x.ClassSessionId.ToString(), Text = x.ClassCode }).ToListAsync();
        if (model.IsParent && !filter.StudentId.HasValue && model.Students.Count > 0) filter.StudentId = int.Parse(model.Students[0].Value);
        if (filter.StudentId.HasValue) scope = scope.Where(p => p.StudentProfileId == filter.StudentId);
        if (filter.ClassId.HasValue) scope = scope.Where(p => p.Assessment.ClassSessionId == filter.ClassId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) { var term = filter.Search.Trim(); scope = scope.Where(p => p.Assessment.Title.Contains(term)); }
        model.Total = await scope.CountAsync(); model.Submitted = await scope.CountAsync(p => p.SubmittedAtUtc != null);
        var graded = scope.Where(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null);
        model.Graded = await graded.CountAsync(); model.Average = await graded.Select(p => (decimal?)(p.Marks!.Value / p.Assessment.MaxMarks * 100)).AverageAsync();
        model.Subjects = await graded.GroupBy(p => p.Assessment.ClassSession.Subject.SubjectName).Select(g => new AssessmentSubjectProgress { Subject = g.Key, Completed = g.Count(), Average = g.Average(p => p.Marks!.Value / p.Assessment.MaxMarks * 100) }).OrderBy(x => x.Subject).ToListAsync();
        filter.Page = Math.Min(filter.Page, model.PageCount);
        var ordered = OrderProgress(scope, filter.Sort, AssessmentRules.NowMyt);
        model.Rows = await ordered.Skip((filter.Page - 1) * 25).Take(25).Select(p => new AssessmentResultRow
        {
            AssessmentId = p.AssessmentId,
            StudentId = p.StudentProfileId,
            Title = p.Assessment.Title,
            Subject = p.Assessment.ClassSession.Subject.SubjectName,
            ClassCode = p.Assessment.ClassSession.ClassCode,
            StudentName = p.StudentName,
            StudentNumber = p.StudentNumber,
            DueAtMyt = p.Assessment.DueAtMyt,
            SubmittedAtUtc = p.SubmittedAtUtc,
            MaxMarks = p.Assessment.MaxMarks,
            Released = p.Assessment.ResultsPublishedAtUtc != null,
            Marks = p.Assessment.ResultsPublishedAtUtc != null ? p.Marks : null,
            Feedback = p.Assessment.ResultsPublishedAtUtc != null ? p.Feedback : null,
            GradedAtUtc = p.Assessment.ResultsPublishedAtUtc != null ? p.GradedAtUtc : null
        }).ToListAsync(); return model;
    }
    // Always order the authorised query before paging. Hidden grades never affect result ordering.
    public static IOrderedQueryable<AssessmentParticipant> OrderProgress(IQueryable<AssessmentParticipant> scope, string sort, DateTime now)
    {
        var ordered = sort switch
        {
            "title_asc" => scope.OrderBy(p => p.Assessment.Title),
            "title_desc" => scope.OrderByDescending(p => p.Assessment.Title),
            "class_asc" => scope.OrderBy(p => p.Assessment.ClassSession.ClassCode).ThenBy(p => p.Assessment.ClassSession.Subject.SubjectName),
            "class_desc" => scope.OrderByDescending(p => p.Assessment.ClassSession.ClassCode).ThenByDescending(p => p.Assessment.ClassSession.Subject.SubjectName),
            "student_asc" => scope.OrderBy(p => p.StudentName).ThenBy(p => p.StudentNumber),
            "student_desc" => scope.OrderByDescending(p => p.StudentName).ThenByDescending(p => p.StudentNumber),
            "status_asc" => scope.OrderBy(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null ? 0 : p.SubmittedAtUtc != null ? 1 : now >= p.Assessment.DueAtMyt ? 2 : 3),
            "status_desc" => scope.OrderByDescending(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null ? 0 : p.SubmittedAtUtc != null ? 1 : now >= p.Assessment.DueAtMyt ? 2 : 3),
            "result_asc" => scope.OrderBy(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null ? 0 : 1)
                .ThenBy(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null ? (decimal?)(p.Marks.Value / p.Assessment.MaxMarks * 100) : null),
            "result_desc" => scope.OrderBy(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null ? 0 : 1)
                .ThenByDescending(p => p.Assessment.ResultsPublishedAtUtc != null && p.Marks != null ? (decimal?)(p.Marks.Value / p.Assessment.MaxMarks * 100) : null),
            "due_desc" => scope.OrderByDescending(p => p.Assessment.DueAtMyt),
            "updated_desc" => scope.OrderByDescending(p => p.Assessment.UpdatedAtUtc),
            _ => scope.OrderBy(p => p.Assessment.DueAtMyt)
        };
        return ordered.ThenBy(p => p.AssessmentParticipantId);
    }
    public static bool ValidFiles(IReadOnlyList<MaterialFile> files, int retained = 0, long retainedBytes = 0) => retained >= 0 && retainedBytes >= 0 && files.Count + retained <= 10 && files.Sum(f => (long)f.Content.Length) + retainedBytes <= 31457280
        && files.All(f => f.Content.Length > 0 && f.Content.Length <= MaterialFileService.MaxBytes && MaterialFileService.Matches(Path.GetExtension(f.Name), f.Content));
    private async Task<string?> Write(Func<Task<string?>> operation)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try { var error = await operation(); if (error != null) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return error; } await tx.CommitAsync(); return null; }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "This record changed. Reload before trying again."; }
        catch (Exception e) when (e is DbUpdateException or SqlException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "The change could not be saved. Reload and try again; reselect files if necessary."; }
    }
}
