using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Models.Progress;
public sealed class ProgressSyllabus
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int AcademicYear { get; set; }
    public DateTime TrackingFrom { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public Subject Subject { get; set; } = null!;
    public List<ProgressTopic> Topics { get; set; } = [];
}
public sealed class ProgressTopic
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public int Chapter { get; set; }
    public int Subchapter { get; set; }
    [Required,MaxLength(100)] public string ChapterTitle { get; set; } = "";
    [Required,MaxLength(100)] public string Title { get; set; } = "";
    public ProgressSyllabus Syllabus { get; set; } = null!;
}
public sealed class ProgressClassState
{
    public int ClassSessionId { get; set; }
    public int SyllabusId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ClassSession ClassSession { get; set; } = null!;
    public ProgressSyllabus Syllabus { get; set; } = null!;
}
public sealed class ProgressTopicState
{
    public int ClassSessionId { get; set; }
    public int TopicId { get; set; }
    public int Percent { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ProgressClassState ClassState { get; set; } = null!;
    public ProgressTopic Topic { get; set; } = null!;
}
public sealed class ProgressLessonRecord
{
    public int Id { get; set; }
    public int ClassSessionId { get; set; }
    public DateTime LessonDate { get; set; }
    public int ChangeKey { get; set; }
    public DateTime OriginalDate { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    [Required,MaxLength(450)] public string TutorUserId { get; set; } = "";
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool SubmittedLate { get; set; }
    [MaxLength(500)] public string Notes { get; set; } = "";
    [MaxLength(500)] public string LateReason { get; set; } = "";
    public ProgressClassState ClassState { get; set; } = null!;
}
public sealed class ProgressChange
{
    public long Id { get; set; }
    public int ClassSessionId { get; set; }
    public int? LessonRecordId { get; set; }
    public int? TopicId { get; set; }
    public int? Before { get; set; }
    public int? After { get; set; }
    [Required,MaxLength(450)] public string ActorUserId { get; set; } = "";
    public DateTime RecordedAtUtc { get; set; }
    [MaxLength(500)] public string Note { get; set; } = "";
    [MaxLength(500)] public string LateReason { get; set; } = "";
    public ProgressClassState ClassState { get; set; } = null!;
    public ProgressLessonRecord? LessonRecord { get; set; }
    public ProgressTopic? Topic { get; set; }
}
