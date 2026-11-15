using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Video;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Video;
namespace SecureSmartTuitionSystem.Services.Video;

public sealed class VideoLessonService(ApplicationDbContext db)
{
    public async Task<VideoClassModel> ClassesAsync(ClaimsPrincipal user)
    {
        var videos = Visible(user);
        return new VideoClassModel {
            IsTutor = IsTutor(user), IsAdmin = user.IsInRole("Admin"),
            Classes = await Access.Classes(user).OrderBy(c => c.ClassCode).Select(c => new VideoClassRow {
                Id = c.ClassSessionId, Code = c.ClassCode, Name = c.ClassName ?? "",
                Subject = c.Subject.AcademicLevel + " · " + c.Subject.SubjectName,
                Recordings = videos.Count(v => v.ClassSessionId == c.ClassSessionId)
            }).ToListAsync()
        };
    }

    // Use the current academic roster, not a historical obligation or attendance measure.
    // Match student video class access, then de-duplicate students before joining progress.
    public IQueryable<VideoViewingRow> ViewingRows(int classId, int videoId, DateTime today)
    {
        return db.StudentProfiles.AsNoTracking().Where(s => s.IsActive && s.ApplicationUserId != null
            && db.StudentEnrollments.Any(e => e.StudentProfileId == s.StudentProfileId
                && e.ClassSessionId == classId && e.StartDate <= today && e.StartDate.Year == today.Year
                && (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop)
                && (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > today)
                && (!e.EndedAt.HasValue || e.EndedAt > today)))
            .Select(s => new VideoViewingRow {
                StudentId = s.StudentNumber, Name = s.FullName,
                Position = db.VideoWatchProgress.Where(p => p.RecordedLessonId == videoId && p.StudentUserId == s.ApplicationUserId).Select(p => (double?)p.PositionSeconds).FirstOrDefault() ?? 0,
                Played = db.VideoWatchProgress.Where(p => p.RecordedLessonId == videoId && p.StudentUserId == s.ApplicationUserId).Select(p => (double?)p.WatchedSeconds).FirstOrDefault() ?? 0,
                Watched = db.VideoWatchProgress.Any(p => p.RecordedLessonId == videoId && p.StudentUserId == s.ApplicationUserId && p.CompletedAtUtc != null),
                LastActivityUtc = db.VideoWatchProgress.Where(p => p.RecordedLessonId == videoId && p.StudentUserId == s.ApplicationUserId && (p.PositionSeconds > 0 || p.WatchedSeconds > 0 || p.CompletedAtUtc != null)).Select(p => (DateTime?)p.UpdatedAtUtc).FirstOrDefault()
            });
    }

    public async Task<VideoViewingModel?> ViewingAsync(ClaimsPrincipal user, int id, string? version, VideoViewingFilter filter)
    {
        if (!IsTutor(user)) return null;
        var lesson = await Visible(user).Where(v => v.RecordedLessonId == id && v.StorageKey == version)
            .Select(v => new { v.ClassSessionId, v.ClassSession.Status, v.ClassSession.StartDate, v.ClassSession.EndDate }).SingleOrDefaultAsync();
        if (lesson == null) return null;
        var today = ClassScheduleService.Today;
        var query = ViewingRows(lesson.ClassSessionId, id, today);
        if (lesson.Status != ClassSessionStatus.Active || lesson.StartDate > today || lesson.EndDate < today)
            query = query.Where(s => false);
        var model = new VideoViewingModel { Filter = filter, UpdatedAtUtc = DateTime.UtcNow };
        var totals = await query.GroupBy(s => 1).Select(g => new {
            Eligible = g.Count(), Watched = g.Count(s => s.Watched),
            InProgress = g.Count(s => !s.Watched && (s.Position > 0 || s.Played > 0))
        }).SingleOrDefaultAsync();
        model.Eligible = totals?.Eligible ?? 0;
        model.Watched = totals?.Watched ?? 0;
        model.InProgress = totals?.InProgress ?? 0;
        if (!string.IsNullOrWhiteSpace(filter.Search)) {
            var term = filter.Search.Trim(); query = query.Where(s => s.Name.Contains(term) || s.StudentId.Contains(term));
        }
        query = filter.Status switch {
            "watched" => query.Where(s => s.Watched),
            "progress" => query.Where(s => !s.Watched && (s.Position > 0 || s.Played > 0)),
            "not" => query.Where(s => !s.Watched && s.Position <= 0 && s.Played <= 0),
            _ => query
        };
        model.Total = await query.CountAsync(); filter.Page = Math.Min(filter.Page, model.PageCount);
        model.Rows = await query.OrderBy(s => s.Name).ThenBy(s => s.StudentId).Skip((filter.Page - 1) * 25).Take(25).ToListAsync();
        return model;
    }
    private LearningMaterialService Access => new(db);
    public static bool IsTutor(ClaimsPrincipal user) => LearningMaterialService.IsTutor(user);
    public static bool Staff(ClaimsPrincipal user) => user.IsInRole("Admin") || IsTutor(user);
    private static string Own(ClaimsPrincipal user) => LearningMaterialService.UserId(user);
    public IQueryable<RecordedLesson> Visible(ClaimsPrincipal user)
    {
        var classes = Access.Classes(user).Select(c => c.ClassSessionId);
        var query = db.Set<RecordedLesson>().AsNoTracking().Where(x => classes.Contains(x.ClassSessionId));
        return Staff(user) ? query : VideoAvailability.ForStudents(query, DateTime.UtcNow);
    }
    public IQueryable<RecordedLesson> Manage(ClaimsPrincipal user)
    {
        var classes = Access.Classes(user, true).Select(c => c.ClassSessionId); var id = Own(user);
        return db.Set<RecordedLesson>().Where(x => IsTutor(user) && x.UploadedByUserId == id && classes.Contains(x.ClassSessionId));
    }
    public static IOrderedQueryable<RecordedLesson> Order(IQueryable<RecordedLesson> query, string sort)
    {
        var cutoff = DateTime.UtcNow.AddDays(-VideoAvailability.ViewingDays);
        return sort switch
    {
        "date_asc" => query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.RecordedLessonId),
        "title_asc" => query.OrderBy(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "title_desc" => query.OrderByDescending(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "class_asc" => query.OrderBy(x => x.ClassSession.ClassCode).ThenBy(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "class_desc" => query.OrderByDescending(x => x.ClassSession.ClassCode).ThenBy(x => x.Title).ThenBy(x => x.RecordedLessonId),
        "status_asc" => query.OrderBy(x => !x.IsActive ? 2 : x.CreatedAtUtc <= cutoff ? 1 : 0).ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.RecordedLessonId),
        "status_desc" => query.OrderByDescending(x => !x.IsActive ? 2 : x.CreatedAtUtc <= cutoff ? 1 : 0).ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.RecordedLessonId),
        _ => query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.RecordedLessonId)
    };
    }
    private IQueryable<VideoRow> Rows(IQueryable<RecordedLesson> query, ClaimsPrincipal user)
    {
        var id = Own(user); var manage = Manage(user).Select(x => x.RecordedLessonId);
        var admin = user.IsInRole("Admin");
        var cutoff = DateTime.UtcNow.AddDays(-VideoAvailability.ViewingDays);
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
            PlaybackVersion = x.StorageKey,
            FileSize = x.FileSize,
            Duration = x.DurationSeconds,
            Date = x.CreatedAtUtc.AddHours(8),
            CreatedAtUtc = x.CreatedAtUtc,
            IsActive = x.IsActive,
            IsExpired = x.CreatedAtUtc <= cutoff,
            CanManage = manage.Contains(x.RecordedLessonId),
            CanChangeState = admin || (!x.InactivatedByAdmin && manage.Contains(x.RecordedLessonId)),
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
    public static IQueryable<RecordedLesson> UploadedInMonth(IQueryable<RecordedLesson> query, DateTime month)
    {
        var startUtc = DateTime.SpecifyKind(new DateTime(month.Year, month.Month, 1).AddHours(-8), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(new DateTime(month.Year, month.Month, 1).AddMonths(1).AddHours(-8), DateTimeKind.Utc);
        return query.Where(x => x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc);
    }
    public async Task<VideoIndexModel> IndexAsync(ClaimsPrincipal user, VideoFilter filter)
    {
        var model = new VideoIndexModel { Filter = filter, IsStaff = Staff(user), IsTutor = IsTutor(user), IsAdmin = user.IsInRole("Admin"), Classes = await Access.ChoicesAsync(user, false) };
        if (model.IsAdmin) model.Tutors = await Visible(user).Select(v => new { v.UploadedByUserId, v.UploadedByUser.FullName }).Distinct().OrderBy(v => v.FullName).Select(v => new SelectListItem { Value = v.UploadedByUserId, Text = v.FullName ?? "Tutor" }).ToListAsync();
        model.Subjects = await Access.Classes(user).Select(c => new { c.SubjectId, c.Subject.SubjectName }).Distinct().OrderBy(x => x.SubjectName).Select(x => new SelectListItem { Value = x.SubjectId.ToString(), Text = x.SubjectName }).ToListAsync();
        var query = Visible(user);
        if (model.IsAdmin && !string.IsNullOrWhiteSpace(filter.TutorId)) query = query.Where(x => x.UploadedByUserId == filter.TutorId);
        if (filter.ClassId.HasValue) query = query.Where(x => x.ClassSessionId == filter.ClassId);
        if (filter.SubjectId.HasValue) query = query.Where(x => x.ClassSession.SubjectId == filter.SubjectId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) { var term = filter.Search.Trim(); query = query.Where(x => x.Title.Contains(term)); }
        if (!string.IsNullOrEmpty(filter.Month) && DateTime.TryParseExact(filter.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month)) query = UploadedInMonth(query, month);
        if (model.IsStaff && filter.Status != "all") {
            var cutoff = DateTime.UtcNow.AddDays(-VideoAvailability.ViewingDays);
            query = filter.Status switch {
                "active" => query.Where(x => x.IsActive && x.CreatedAtUtc > cutoff),
                "expired" => query.Where(x => x.IsActive && x.CreatedAtUtc <= cutoff),
                _ => query.Where(x => !x.IsActive)
            };
        }
        query = ApplyWatchFilter(query, user, filter.Watch);
        model.Total = await query.CountAsync(); filter.Page = Math.Min(filter.Page, model.PageCount);
        model.Rows = await Rows(Order(query, filter.Sort).Skip((filter.Page - 1) * 25).Take(25), user).ToListAsync(); return model;
    }
    public async Task<VideoPlayModel?> PlayAsync(ClaimsPrincipal user, int id)
    {
        var row = await Rows(Visible(user).Where(x => x.RecordedLessonId == id), user).SingleOrDefaultAsync(); if (row == null) return null;
        if (Staff(user)) return new VideoPlayModel { Lesson = row, IsTutor = IsTutor(user) };
        var playlist = Visible(user).Where(x => x.ClassSessionId == row.ClassId && x.IsActive);
        return new VideoPlayModel { Lesson = row, IsStudent = user.IsInRole("Student") && !Staff(user), PlaylistLimited = await playlist.CountAsync() > 100, Playlist = await Rows(Order(playlist, "date_desc").Take(100), user).ToListAsync() };
    }
    public async Task<VideoForm?> FormAsync(ClaimsPrincipal user, int id)
    {
        VideoForm? form;
        if (id == 0) form = new();
        else form = await Manage(user).AsNoTracking().Where(x => x.RecordedLessonId == id).Select(x => new VideoForm { Id = x.RecordedLessonId, ClassSessionId = x.ClassSessionId, Title = x.Title, Description = x.Description, CreatedAtUtc = x.CreatedAtUtc, RowVersion = Convert.ToBase64String(x.RowVersion), CurrentFileName = x.OriginalFileName, CanReplaceVideo = !x.InactivatedByAdmin }).SingleOrDefaultAsync();
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
        return null;
    }
    public async Task<(int Id, string? Error, string? PreviousKey)> SaveAsync(ClaimsPrincipal user, VideoForm form, (string Key, long Size, double Duration)? file)
    {
        var error = Validate(form, ClassScheduleService.Today); if (error != null) return (0, error, null);
        if (form.Id > 0 && file.HasValue && !form.ReplacementConfirmed) return (0, "Confirm the replacement before saving.", null);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        RecordedLesson lesson;
        string? previousKey = null;
        try
        {
            var cls = await Access.Classes(user, true).Where(c => c.ClassSessionId == form.ClassSessionId).Select(c => new { c.ClassSessionId, c.StartDate, c.EndDate }).SingleOrDefaultAsync();
            if (!IsTutor(user) || cls == null) return (0, "This assigned class is no longer available. Choose another class.", null);
            if (form.Id == 0)
            {
                if (file == null) return (0, "Please select an MP4 video.", null);
                lesson = new() { ClassSessionId = cls.ClassSessionId, UploadedByUserId = Own(user), CreatedAtUtc = DateTime.UtcNow, RecordingDate = ClassScheduleService.Today };
                db.Add(lesson);
            }
            else
            {
                var found = await Manage(user).Where(x => x.RecordedLessonId == form.Id).AsTracking().SingleOrDefaultAsync();
                if (found == null) return (0, "This recording can no longer be edited.", null); lesson = found;
                if (lesson.ClassSessionId != form.ClassSessionId) return (0, "The recording class cannot be changed.", null);
                if (!AssessmentRules.Version(lesson.RowVersion, form.RowVersion)) return (0, "This recording changed. Reload before saving.", null);
                if (file.HasValue && lesson.InactivatedByAdmin) return (0, "Only an administrator can restore this recording before it can be replaced.", null);
                if (!file.HasValue && lesson.Title == form.Title.Trim() && lesson.Description == (string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim())) return (0, "No changes to save.", null);
            }
            if (file is { } uploaded)
            {
                if (form.Id > 0)
                {
                    previousKey = lesson.StorageKey;
                    await db.VideoWatchProgress.Where(p => p.RecordedLessonId == lesson.RecordedLessonId).ExecuteDeleteAsync();
                }
                var name = Path.GetFileName((form.VideoFile?.FileName ?? "recording.mp4").Replace('\\', '/'));
                lesson.OriginalFileName = name[..Math.Min(180, name.Length)];
                lesson.StorageKey = uploaded.Key;
                lesson.FileSize = uploaded.Size;
                lesson.DurationSeconds = uploaded.Duration;
            }
            lesson.Title = form.Title.Trim(); lesson.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(); lesson.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception e) when (e is DbUpdateException or SqlException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return (0, "The recording could not be saved. Reload and try again.", null); }
        await tx.CommitAsync();
        return (lesson.RecordedLessonId, null, previousKey);
    }
    public async Task<string?> StateAsync(ClaimsPrincipal user, int id, string? version, bool active)
    {
        var admin = user.IsInRole("Admin");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var recordings = admin ? Visible(user) : Manage(user);
            var lesson = await recordings.AsTracking().SingleOrDefaultAsync(x => x.RecordedLessonId == id);
            if (lesson == null) return "This recording is not available for management.";
            if (!AssessmentRules.Version(lesson.RowVersion, version)) return "The recording changed. Reload and try again.";
            if (lesson.IsActive == active) return "The recording already has this status.";
            if (!admin && lesson.InactivatedByAdmin) return "Only an administrator can restore this recording.";

            lesson.IsActive = active;
            lesson.InactivatedByAdmin = admin && !active;
            lesson.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }
        catch (Exception e) when (e is DbUpdateException or SqlException) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return "The recording changed. Reload before trying again."; }
    }
    public async Task<bool> ProgressAsync(ClaimsPrincipal user, VideoProgressForm form)
    {
        if (Staff(user) || !user.IsInRole("Student") || !double.IsFinite(form.Position) || !double.IsFinite(form.Played)) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var lesson = await Visible(user).Where(x => x.RecordedLessonId == form.Id).Select(x => new { x.DurationSeconds, x.StorageKey }).SingleOrDefaultAsync();
            if (lesson == null || lesson.StorageKey != form.PlaybackVersion) return false;
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
