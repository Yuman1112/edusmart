using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace SecureSmartTuitionSystem.ViewModels.Video;
public sealed class VideoFilter
{
    [StringLength(100)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int? ClassId { get; set; }
    [Range(1, int.MaxValue)] public int? SubjectId { get; set; }
    [RegularExpression(@"^(20\d{2}|2100)-(0[1-9]|1[0-2])$")] public string? Month { get; set; }
    [Required, RegularExpression("^(active|inactive|all)$")] public string Status { get; set; } = "active";
    [Required, RegularExpression("^(date_desc|date_asc|title_asc|title_desc|class_asc|class_desc|status_asc|status_desc)$")] public string Sort { get; set; } = "date_desc";
    [Required, RegularExpression("^(all|not|progress|watched)$")] public string Watch { get; set; } = "all";
    [Range(1, 100000)] public int Page { get; set; } = 1;
}
public sealed class VideoForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Please select an assigned class."), Range(1, int.MaxValue)] public int? ClassSessionId { get; set; }
    [Required(ErrorMessage = "Please enter a lesson title."), StringLength(150)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    [Required(ErrorMessage = "Please select the recording date.")] public DateTime? RecordingDate { get; set; }
    public IFormFile? VideoFile { get; set; }
    [StringLength(100)] public string? RowVersion { get; set; }
    public List<SelectListItem> Classes { get; set; } = [];
    public string SubjectLabel { get; set; } = "";
    public string CurrentFileName { get; set; } = "";
}
public sealed class VideoRow
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string ClassCode { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Level { get; set; } = "";
    public string Tutor { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public double Duration { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool CanManage { get; set; }
    public bool Watched { get; set; }
    public double Position { get; set; }
    public string RowVersion { get; set; } = "";
    public string WatchState => Watched ? "watched" : Position > 0 ? "progress" : "not";
    public string PositionText => TimeSpan.FromSeconds(Math.Max(0, Position)).ToString(Position >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    public double PositionPercent => Duration > 0 ? Math.Clamp(Position / Duration * 100, 0, 100) : 0;
    public string DurationText => TimeSpan.FromSeconds(Duration).ToString(Duration >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}
public sealed class VideoIndexModel
{
    public VideoFilter Filter { get; set; } = new();
    public bool IsStaff { get; set; }
    public bool IsTutor { get; set; }
    public int Total { get; set; }
    public int PageCount => Math.Max(1, (Total + 24) / 25);
    public List<SelectListItem> Classes { get; set; } = [];
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<VideoRow> Rows { get; set; } = [];
}
public sealed class VideoPlayModel
{
    public VideoRow Lesson { get; set; } = new();
    public List<VideoRow> Playlist { get; set; } = [];
    public bool PlaylistLimited { get; set; }
    public bool IsStudent { get; set; }
}
public sealed class VideoProgressForm
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Range(0, 28800)] public double Position { get; set; }
    [Range(0, 60)] public double Played { get; set; }
}
