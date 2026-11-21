using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Progress;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.ViewModels.Progress;
public sealed class ProgressFilter
{
    [Range(2000,2100)] public int Year { get; set; } = DateTime.UtcNow.AddHours(8).Year;
    [Range(1,12)] public int Month { get; set; } = DateTime.UtcNow.AddHours(8).Month;
    [Range(1,int.MaxValue)] public int? SubjectId { get; set; }
    [Range(1,int.MaxValue)] public int? ClassId { get; set; }
}
public sealed record ProgressSummary(int Id,string Code,string Name,string Subject,string Tutor,int Topics,int Done,decimal? Average,int Expected,int Submitted,int Missing,int Late);
public sealed class ProgressIndex
{
    public ProgressFilter Filter { get; set; } = new();
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<SelectListItem> Classes { get; set; } = [];
    public List<ProgressSummary> Rows { get; set; } = [];
}
public sealed class ChapterInput
{
    [StringLength(100)] public string? Title { get; set; }
    [StringLength(2100)] public string? Subchapters { get; set; }
}
public sealed class SyllabusForm
{
    [Range(1,int.MaxValue)] public int ClassId { get; set; }
    [DataType(DataType.Date)] public DateTime TrackingFrom { get; set; }
    [StringLength(100)] public string? Version { get; set; }
    public List<ChapterInput> Chapters { get; set; } = [];
    public string ClassLabel { get; set; } = "";
    public bool Locked { get; set; }
}
public sealed class TopicInput { public int TopicId { get; set; } public int Percent { get; set; } }
public sealed class LessonProgressForm
{
    [Range(1,int.MaxValue)] public int ClassId { get; set; }
    [DataType(DataType.Date)] public DateTime Date { get; set; }
    [Range(0,int.MaxValue)] public int ChangeKey { get; set; }
    [StringLength(100)] public string? Version { get; set; }
    [Required,StringLength(500,MinimumLength=3)] public string Notes { get; set; } = "";
    [StringLength(500)] public string? LateReason { get; set; }
    public bool ConfirmCompleted { get; set; }
    public List<TopicInput> Topics { get; set; } = [];
}
public sealed class ProgressDetails
{
    public ClassSession Class { get; set; } = null!;
    public ProgressSyllabus? Syllabus { get; set; }
    public ProgressClassState? State { get; set; }
    public List<ProgressTopicState> Progress { get; set; } = [];
    public List<LessonItem> Lessons { get; set; } = [];
    public List<ProgressLessonRecord> Records { get; set; } = [];
    public List<ProgressChange> History { get; set; } = [];
    public Dictionary<string,string> Actors { get; set; } = [];
    public int HistoryPage { get; set; } = 1;
    public int HistoryTotal { get; set; }
    public int HistoryPages => Math.Max(1,(int)Math.Ceiling(HistoryTotal/25d));
    public int Month { get; set; }
    public LessonItem? Selected { get; set; }
    public bool CanSave { get; set; }
    public bool IsLate { get; set; }
    public LessonProgressForm Form { get; set; } = new();
}
public sealed class ReopenProgressForm
{
    [Range(0,75)] public int TargetPercent { get; set; } = 75;
    [Range(1,int.MaxValue)] public int ClassId { get; set; }
    [Range(1,int.MaxValue)] public int TopicId { get; set; }
    [Required,StringLength(100)] public string Version { get; set; } = "";
    [Required,StringLength(500,MinimumLength=10)] public string Reason { get; set; } = "";
}
