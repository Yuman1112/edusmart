using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Learning;
namespace SecureSmartTuitionSystem.Data;

public static class LearningModelConfiguration
{
    public static void ConfigureLearningMaterials(this ModelBuilder builder)
    {
        builder.Entity<LearningMaterialAttachment>(e =>
        {
            e.ToTable("LearningMaterialAttachments", t => t.HasCheckConstraint("CK_LearningMaterialAttachments_FileSize", "[FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([FileContent]) = [FileSize]"));
            e.HasKey(x => x.LearningMaterialAttachmentId);
            e.HasOne(x => x.LearningMaterial).WithMany().HasForeignKey(x => x.LearningMaterialId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.FileContent).HasColumnType("varbinary(max)");
        });
        builder.Entity<LearningMaterial>(e =>
        {
            e.ToTable("LearningMaterials", table =>
                table.HasCheckConstraint("CK_LearningMaterials_FileSize", "[FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([FileContent]) = [FileSize]"));
            e.HasKey(x => x.LearningMaterialId);
            e.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.FileContent).HasColumnType("varbinary(max)");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.ClassSessionId, x.IsActive, x.CreatedAtUtc });
        });
    }
}
