using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Attendance;
namespace SecureSmartTuitionSystem.Data;
public static class AttendanceModelConfiguration
{
    public static void ConfigureAttendance(this ModelBuilder builder)
    {
        builder.Entity<AttendanceLesson>(entity =>
        {
            entity.ToTable("AttendanceLessons");
            entity.HasIndex(x => new { x.ClassSessionId, x.OriginalDate, x.IsReplacement }).IsUnique();
            entity.HasIndex(x => new { x.TutorUserId, x.LessonDate });
            entity.Property(x => x.OriginalDate).HasColumnType("date");
            entity.Property(x => x.LessonDate).HasColumnType("date");
            entity.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClassLessonChange).WithMany().HasForeignKey(x => x.ClassLessonChangeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TutorUser).WithMany().HasForeignKey(x => x.TutorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Records).WithOne(x => x.AttendanceLesson).HasForeignKey(x => x.AttendanceLessonId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(x => new { x.AttendanceLessonId, x.StudentProfileId }).IsUnique();
            entity.HasOne(x => x.StudentProfile).WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("AttendanceRecords", table => table.HasCheckConstraint(
                "CK_AttendanceRecord_Status", "[Status] IS NULL OR [Status] IN (1,2,3)"));
        });
        builder.Entity<AttendanceCorrectionRequest>(entity =>
        {
            entity.ToTable("AttendanceCorrectionRequests", table =>
            {
                table.HasCheckConstraint("CK_AttendanceCorrection_Decision", "[Decision] IN (0,1,2)");
                table.HasCheckConstraint("CK_AttendanceCorrection_Status", "[RequestedStatus] IN (1,2,3)");
            });
            entity.HasIndex(x => x.AttendanceRecordId).IsUnique().HasFilter("[Decision] = 0");
            entity.HasIndex(x => new { x.RequestedByUserId, x.RequestedAtUtc });
            entity.HasOne(x => x.AttendanceRecord).WithMany().HasForeignKey(x => x.AttendanceRecordId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}