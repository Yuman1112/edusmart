using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Enrollment;
// One exception per original weekly lesson. The weekly class itself stays active.
public sealed class ClassLessonChange
{
    public int ClassLessonChangeId { get; set; }
    public int ClassSessionId { get; set; }
    public DateTime OriginalDate { get; set; }
    public TimeOnly OriginalStartTime { get; set; }
    public TimeOnly OriginalEndTime { get; set; }
    [Required, MaxLength(500)] public string CancellationReason { get; set; } = "";
    public DateTime CancelledAtUtc { get; set; }
    [Required, MaxLength(450)] public string ChangedByUserId { get; set; } = "";
    public DateTime? ReplacementDate { get; set; }
    public TimeOnly? ReplacementStartTime { get; set; }
    public TimeOnly? ReplacementEndTime { get; set; }
    [MaxLength(250)] public string? ReplacementVenue { get; set; }
    // Original roster is preserved even if students subsequently withdraw or join.
    public string StudentIdsJson { get; set; } = "[]";
    public string RecipientIdsJson { get; set; } = "[]";
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ClassSession ClassSession { get; set; } = null!;
}
