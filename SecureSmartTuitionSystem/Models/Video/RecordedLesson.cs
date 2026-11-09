using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Video;

public sealed class RecordedLesson
{
    public int RecordedLessonId { get; set; }

    public int ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;

    [MaxLength(450)]
    public string UploadedByUserId { get; set; } = "";

    public ApplicationUser UploadedByUser { get; set; } = null!;

    [MaxLength(150)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime RecordingDate { get; set; }

    [MaxLength(40)]
    public string StorageKey { get; set; } = "";

    [MaxLength(180)]
    public string OriginalFileName { get; set; } = "";

    public long FileSize { get; set; }

    public double DurationSeconds { get; set; }

    public bool IsActive { get; set; } = true;
    public bool InactivatedByAdmin { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

public sealed class VideoWatchProgress
{
    public int VideoWatchProgressId { get; set; }

    public int RecordedLessonId { get; set; }
    public RecordedLesson RecordedLesson { get; set; } = null!;

    [MaxLength(450)]
    public string StudentUserId { get; set; } = "";

    public ApplicationUser StudentUser { get; set; } = null!;

    public double PositionSeconds { get; set; }

    public double WatchedSeconds { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
