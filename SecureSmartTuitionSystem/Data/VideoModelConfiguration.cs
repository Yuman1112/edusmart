using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Video;
namespace SecureSmartTuitionSystem.Data;
public static class VideoModelConfiguration
{
    public static void ConfigureVideoPlayback(this ModelBuilder builder)
    {
        builder.Entity<RecordedLesson>(b => {
            b.ToTable("RecordedLessons", t => {
                t.HasCheckConstraint("CK_RecordedLesson_Size", "[FileSize] > 0 AND [FileSize] <= 209715200");
                t.HasCheckConstraint("CK_RecordedLesson_Duration", "[DurationSeconds] > 0 AND [DurationSeconds] <= 28800");
            });
            b.HasKey(x => x.RecordedLessonId);
            b.Property(x => x.RecordingDate).HasColumnType("date");
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.StorageKey).IsUnique();
            b.HasIndex(x => new { x.ClassSessionId, x.IsActive, x.RecordingDate });
        });
        builder.Entity<VideoWatchProgress>(b => {
            b.ToTable("VideoWatchProgress", t => t.HasCheckConstraint("CK_VideoWatch_Seconds", "[PositionSeconds] >= 0 AND [WatchedSeconds] >= 0"));
            b.HasKey(x => x.VideoWatchProgressId);
            b.HasOne(x => x.RecordedLesson).WithMany().HasForeignKey(x => x.RecordedLessonId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.StudentUser).WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RecordedLessonId, x.StudentUserId }).IsUnique();
        });
    }
}
