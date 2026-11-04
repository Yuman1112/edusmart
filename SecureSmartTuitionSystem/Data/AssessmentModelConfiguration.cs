using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Learning;
namespace SecureSmartTuitionSystem.Data;
public static class AssessmentModelConfiguration
{
    public static void ConfigureAssessments(this ModelBuilder builder)
    {
        builder.Entity<Assessment>(b => {
            b.ToTable("Assessments", t => {
                t.HasCheckConstraint("CK_Assessment_Marks", "[MaxMarks] > 0 AND [MaxMarks] <= 10000");
                t.HasCheckConstraint("CK_Assessment_Dates", "[DueAtMyt] > [OpensAtMyt]");
                t.HasCheckConstraint("CK_Assessment_Kind", "[Kind] IN (1,2,3)");
            });
            b.HasKey(x => x.AssessmentId); b.Property(x => x.MaxMarks).HasPrecision(7, 2);
            b.Property(x => x.OpensAtMyt).HasColumnType("datetime2"); b.Property(x => x.DueAtMyt).HasColumnType("datetime2");
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ClassSessionId, x.IsActive, x.DueAtMyt });
        });
        builder.Entity<AssessmentParticipant>(b => {
            b.ToTable("AssessmentParticipants", t => {
                t.HasCheckConstraint("CK_AssessmentParticipant_Marks", "[Marks] IS NULL OR ([Marks] >= 0 AND [Marks] <= 10000)");
                t.HasCheckConstraint("CK_AssessmentParticipant_Grade", "([Marks] IS NULL AND [GradedAtUtc] IS NULL) OR ([SubmittedAtUtc] IS NOT NULL AND [Marks] IS NOT NULL AND [GradedAtUtc] IS NOT NULL)");
            });
            b.HasKey(x => x.AssessmentParticipantId); b.Property(x => x.Marks).HasPrecision(7, 2); b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.AssessmentId, x.StudentProfileId }).IsUnique();
            b.HasOne(x => x.Assessment).WithMany(x => x.Participants).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.StudentProfile).WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AssessmentFile>(b => {
            b.ToTable("AssessmentFiles", t => t.HasCheckConstraint("CK_AssessmentFile_Size", "[FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([Content]) = [FileSize]"));
            b.HasKey(x => x.AssessmentFileId); b.HasIndex(x => new { x.AssessmentId, x.AssessmentParticipantId });
            b.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.AssessmentParticipantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
