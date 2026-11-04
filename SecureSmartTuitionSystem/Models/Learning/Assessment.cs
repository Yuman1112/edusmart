using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Models.Learning;

public enum AssessmentKind { Assignment = 1, Quiz = 2, Test = 3 }
public sealed class Assessment
{
    public int AssessmentId { get; set; }
    public int ClassSessionId { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = "";
    public AssessmentKind Kind { get; set; } = AssessmentKind.Assignment;
    [StringLength(4000)] public string? Instructions { get; set; }
    public decimal MaxMarks { get; set; } = 100;
    // Classroom dates are wall-clock Malaysia time; audit timestamps are UTC.
    public DateTime OpensAtMyt { get; set; }
    public DateTime DueAtMyt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ResultsPublishedAtUtc { get; set; }
    [Required, StringLength(450)] public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ClassSession ClassSession { get; set; } = null!;
    public ICollection<AssessmentParticipant> Participants { get; set; } = new List<AssessmentParticipant>();
}
public sealed class AssessmentParticipant
{
    public int AssessmentParticipantId { get; set; }
    public int AssessmentId { get; set; }
    public int StudentProfileId { get; set; }
    [Required, StringLength(20)] public string StudentNumber { get; set; } = "";
    [Required, StringLength(100)] public string StudentName { get; set; } = "";
    public DateTime? SubmittedAtUtc { get; set; }
    [StringLength(2000)] public string? SubmissionNote { get; set; }
    public decimal? Marks { get; set; }
    [StringLength(2000)] public string? Feedback { get; set; }
    [StringLength(450)] public string? GradedByUserId { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public Assessment Assessment { get; set; } = null!;
    public StudentProfile StudentProfile { get; set; } = null!;
}
public sealed class AssessmentFile
{
    public int AssessmentFileId { get; set; }
    public int AssessmentId { get; set; }
    // Null means tutor question file; otherwise a student's immutable confirmed submission.
    public int? AssessmentParticipantId { get; set; }
    [Required, StringLength(180)] public string Name { get; set; } = "";
    [Required, StringLength(100)] public string ContentType { get; set; } = "";
    public int FileSize { get; set; }
    [Required] public byte[] Content { get; set; } = [];
    public Assessment Assessment { get; set; } = null!;
    public AssessmentParticipant? Participant { get; set; }
}
