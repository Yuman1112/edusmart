using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Video;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Video;
namespace SecureSmartTuitionSystem.Services.Video;

public sealed class VideoLessonService(ApplicationDbContext db)
{
    private LearningMaterialService Access => new(db);
    public static bool IsTutor(ClaimsPrincipal user) => LearningMaterialService.IsTutor(user);
    public static bool Staff(ClaimsPrincipal user) => user.IsInRole("Admin") || IsTutor(user);
    private static string Own(ClaimsPrincipal user) => LearningMaterialService.UserId(user);
    public IQueryable<RecordedLesson> Visible(ClaimsPrincipal user)
    {
        var classes = Access.Classes(user).Select(c => c.ClassSessionId);
        var query = db.Set<RecordedLesson>().AsNoTracking().Where(x => classes.Contains(x.ClassSessionId));
        return Staff(user) ? query : query.Where(x => x.IsActive);
    }
    public IQueryable<RecordedLesson> Manage(ClaimsPrincipal user)
    {
        var classes = Access.Classes(user, true).Select(c => c.ClassSessionId); var id = Own(user);
        return db.Set<RecordedLesson>().Where(x => IsTutor(user) && x.UploadedByUserId == id && classes.Contains(x.ClassSessionId));
    }
    public static IOrderedQueryable<RecordedLesson> Order(IQueryable<RecordedLesson> query, string sort) => sort switch
    {
        "date_asc" => query.OrderBy(x => x.RecordingDate).ThenBy(x => x.RecordedLessonId),
        "title_asc" => query.OrderBy(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "title_desc" => query.OrderByDescending(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "class_asc" => query.OrderBy(x => x.ClassSession.ClassCode).ThenBy(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "class_desc" => query.OrderByDescending(x => x.ClassSession.ClassCode).ThenBy(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "status_asc" => query.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.RecordingDate).ThenBy(x => x.RecordedLessonId),
        "status_desc" => query.OrderBy(x => x.IsActive).ThenByDescending(x => x.RecordingDate).ThenBy(x => x.RecordedLessonId),
        _ => query.OrderByDescending(x => x.RecordingDate).ThenByDescending(x => x.RecordedLessonId)
    };
    private IQueryable<VideoRow> Rows(IQueryable<RecordedLesson> query, ClaimsPrincipal user)
    {
        var id = Own(user); var manage = Manage(user).Select(x => x.RecordedLessonId);
        return query.Select(x => new VideoRow
        {
            Id = x.RecordedLessonId,
            ClassId = x.ClassSessionId,
            Title = x.Title,
            Description = x.Description,
            ClassCode = x.ClassSession.ClassCode,
            ClassName = x.ClassSession.ClassName ?? "",
            Subject = x.ClassSession.Subject.SubjectName,
            Level = x.ClassSession.Subject.AcademicLevel,
            Tutor = x.UploadedByUser.FullName ?? "",
            FileName = x.OriginalFileName,
            FileSize = x.FileSize,
            Duration = x.DurationSeconds,
            Date = x.RecordingDate,
            CreatedAtUtc = x.CreatedAtUtc,
            IsActive = x.IsActive,
            CanManage = manage.Contains(x.RecordedLessonId),
            RowVersion = Convert.ToBase64String(x.RowVersion),
            Watched = db.Set<VideoWatchProgress>().Any(p => p.RecordedLessonId == x.RecordedLessonId && p.StudentUserId == id && p.CompletedAtUtc != null),
            Position = db.Set<VideoWatchProgress>().Where(p => p.RecordedLessonId == x.RecordedLessonId && p.StudentUserId == id).Select(p => (double?)p.PositionSeconds).FirstOrDefault() ?? 0
        });
    }
    public IQueryable<RecordedLesson> ApplyWatchFilter(IQueryable<RecordedLesson> query, ClaimsPrincipal user, string watch)
    {
        if (Staff(user) || !user.IsInRole("Student") || watch == "all") return query;
        var id = Own(user);
        var progress = db.Set<VideoWatchProgress>().Where(p => p.StudentUserId == id);
        return watch switch
        {
            "watched" => query.Where(x => progress.Any(p => p.RecordedLessonId == x.RecordedLessonId && p.CompletedAtUtc != null)),
            "progress" => query.Where(x => !progress.Any(p => p.RecordedLessonId == x.RecordedLessonId && p.CompletedAtUtc != null) && progress.Any(p => p.RecordedLessonId == x.RecordedLessonId && p.PositionSeconds > 0)),
            "not" => query.Where(x => !progress.Any(p => p.RecordedLessonId == x.RecordedLessonId && (p.CompletedAtUtc != null || p.PositionSeconds > 0))),
            _ => query
        };
    }
    public async Task<VideoIndexModel> IndexAsync(ClaimsPrincipal user, VideoFilter filter)
    {
        var model = new VideoIndexModel { Filter = filter, IsStaff = Staff(user), IsTutor = IsTutor(user), Classes = await Access.ChoicesAsync(user, false) };
        model.Subjects = await Access.Classes(user).Select(c => new { c.SubjectId, c.Subject.SubjectName }).Distinct().OrderBy(x => x.SubjectName).Select(x => new SelectListItem { Value = x.SubjectId.ToString(), Text = x.SubjectName }).ToListAsync();
        var query = Visible(user);
        if (filter.ClassId.HasValue) query = query.Where(x => x.ClassSessionId == filter.ClassId);
        if (filter.SubjectId.HasValue) query = query.Where(x => x.ClassSession.SubjectId == filter.SubjectId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) { var term = filter.Search.Trim(); query = query.Where(x => x.Title.Contains(term)); }
        if (!string.IsNullOrEmpty(filter.Month) && DateTime.TryParseExact(filter.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month)) { var end = month.AddMonths(1); query = query.Where(x => x.RecordingDate >= month && x.RecordingDate < end); }
        if (model.IsStaff && filter.Status != "all") query = query.Where(x => x.IsActive == (filter.Status == "active"));
        query = ApplyWatchFilter(query, user, filter.Watch);
        model.Total = await query.CountAsync(); filter.Page = Math.Min(filter.Page, model.PageCount);
        model.Rows = await Rows(Order(query, filter.Sort).Skip((filter.Page - 1) * 25).Take(25), user).ToListAsync(); return model;
    }
    public async Task<VideoPlayModel?> PlayAsync(ClaimsPrincipal user, int id)
    {
        var row = await Rows(Visible(user).Where(x => x.RecordedLessonId == id), user).SingleOrDefaultAsync(); if (row == null) return null;
        var playlist = Visible(user).Where(x => x.ClassSessionId == row.ClassId && x.IsActive);
        return new VideoPlayModel { Lesson = row, IsStudent = user.IsInRole("Student") && !Staff(user), PlaylistLimited = await playlist.CountAsync() > 100, Playlist = await Rows(Order(playlist, "date_desc").Take(100), user).ToListAsync() };
    }
    public async Task<VideoForm?> FormAsync(ClaimsPrincipal user, int id)
    {
        VideoForm? form;
        if (id == 0) form = new() { RecordingDate = ClassScheduleService.Today };
        else form = await Manage(user).AsNoTracking().Where(x => x.RecordedLessonId == id).Select(x => new VideoForm { Id = x.RecordedLessonId, ClassSessionId = x.ClassSessionId, Title = x.Title, Description = x.Description, RecordingDate = x.RecordingDate, RowVersion = Convert.ToBase64String(x.RowVersion), CurrentFileName = x.OriginalFileName }).SingleOrDefaultAsync();
        if (form != null) await PopulateAsync(user, form); return form;
    }
    public async Task PopulateAsync(ClaimsPrincipal user, VideoForm form)
    {
        form.Classes = await Access.ChoicesAsync(user, true);
        form.SubjectLabel = await Access.Classes(user, true).Where(c => c.ClassSessionId == form.ClassSessionId).Select(c => c.Subject.AcademicLevel + " · " + c.Subject.SubjectName).FirstOrDefaultAsync() ?? "Follows the selected class";
    }
    public static string? Validate(VideoForm form, DateTime today)
    {
        if (string.IsNullOrWhiteSpace(form.Title)) return "Please enter a lesson title.";
        if (!form.RecordingDate.HasValue || form.RecordingDate.Value.Date < new DateTime(2000, 1, 1) || form.RecordingDate.Value.Date > today.Date) return "Choose a recording date from 2000 up to today (MYT).";
        return null;
    }
    public async Task<(int Id, string? Error)> SaveAsync(ClaimsPrincipal user, VideoForm form, (string Key, long Size, double Duration)? file)
    {
        var error = Validate(form, ClassScheduleService.Today); if (error != null) return (0, error);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var cls = await Access.Classes(user, true).Where(c => c.ClassSessionId == form.ClassSessionId).Select(c => new { c.ClassSessionId, c.StartDate, c.EndDate }).SingleOrDefaultAsync();
            if (!IsTutor(user) || cls == null) return (0, "This assigned class is no longer available. Choose another class.");
            if (cls.StartDate.HasValue && form.RecordingDate!.Value.Date < cls.StartDate.Value.Date || cls.EndDate.HasValue && form.RecordingDate!.Value.Date > cls.EndDate.Value.Date) return (0, "The recording date must fall within the class date range.");
            RecordedLesson lesson;
            if (form.Id == 0)
            {
                if (file == null) return (0, "Please select an MP4 video.");
                lesson = new() { ClassSessionId = cls.ClassSessionId, UploadedByUserId = Own(user), StorageKey = file.Value.Key, FileSize = file.Value.Size, DurationSeconds = file.Value.Duration, OriginalFileName = Path.GetFileName((form.VideoFile?.FileName ?? "recording.mp4").Replace('\\', '/'))[..Math.Min(180, Path.GetFileName((form.VideoFile?.FileName ?? "recording.mp4").Replace('\\', '/')).Length)], CreatedAtUtc = DateTime.UtcNow };
                db.Add(lesson);
            }
            else
            {
                var found = await Manage(user).Where(x => x.RecordedLessonId == form.Id).AsTracking().SingleOrDefaultAsync();
                if (found == null) return (0, "This recording can no longer be edited."); lesson = found;
                if (lesson.ClassSessionId != form.ClassSessionId) return (0, "The recording class cannot be changed.");
                if (!AssessmentRules.Version(lesson.RowVersion, form.RowVersion)) return (0, "This recording changed. Reload before saving.");
                if (lesson.Title == form.Title.Trim() && lesson.Description == (string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim()) && lesson.RecordingDate == form.RecordingDate!.Value.Date) return (0, "No changes to save.");
            }
            lesson.Title = form.Title.Trim(); lesson.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(); lesson.RecordingDate = form.RecordingDate!.Value.Date; lesson.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return (lesson.RecordedLessonId, null);
        }
        catch (Exception e) when (e is DbUpdateException or SqlException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return (0, "The recording could not be saved. Reload and try again."); }
    }
    public async Task<string?> StateAsync(ClaimsPrincipal user, int id, string? version, bool active)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var lesson = await Manage(user).Where(x => x.RecordedLessonId == id).AsTracking().SingleOrDefaultAsync();
            if (lesson == null) return "This recording is not available for management.";
            if (!AssessmentRules.Version(lesson.RowVersion, version)) return "The recording changed. Reload and try again.";
            if (lesson.IsActive == active) return "The recording already has this status.";
            lesson.IsActive = active; lesson.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); await tx.CommitAsync(); return null;
        }
        catch (Exception e) when (e is DbUpdateException or SqlException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "The recording changed. Reload before trying again."; }
    }
    public async Task<bool> ProgressAsync(ClaimsPrincipal user, VideoProgressForm form)
    {
        if (Staff(user) || !user.IsInRole("Student") || !double.IsFinite(form.Position) || !double.IsFinite(form.Played)) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var lesson = await Visible(user).Where(x => x.RecordedLessonId == form.Id).Select(x => new { x.DurationSeconds }).SingleOrDefaultAsync(); if (lesson == null) return false;
            var id = Own(user); var now = DateTime.UtcNow;
            var p = await db.Set<VideoWatchProgress>().SingleOrDefaultAsync(x => x.RecordedLessonId == form.Id && x.StudentUserId == id);
            if (p == null) { p = new() { RecordedLessonId = form.Id, StudentUserId = id, UpdatedAtUtc = now.AddSeconds(-15) }; db.Add(p); }
            p.PositionSeconds = Math.Clamp(form.Position, 0, lesson.DurationSeconds);
            p.WatchedSeconds = Math.Min(lesson.DurationSeconds, p.WatchedSeconds + Math.Min(Math.Clamp(form.Played, 0, 60), Math.Clamp((now - p.UpdatedAtUtc).TotalSeconds * 2 + 2, 0, 60)));
            if (p.WatchedSeconds >= lesson.DurationSeconds * .9 && p.PositionSeconds >= lesson.DurationSeconds * .95) p.CompletedAtUtc ??= now;
            p.UpdatedAtUtc = now; await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
        }
        catch (Exception e) when (e is DbUpdateException or SqlException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return false; }
    }
}
