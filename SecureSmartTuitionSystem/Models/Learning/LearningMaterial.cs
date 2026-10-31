using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Models.Learning;

public sealed class LearningMaterial
{
    public int LearningMaterialId { get; set; }
    public int ClassSessionId { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    [Required, StringLength(180)] public string OriginalFileName { get; set; } = "";
    [Required, StringLength(100)] public string ContentType { get; set; } = "";
    public int FileSize { get; set; }
    [Required] public byte[] FileContent { get; set; } = [];
    public bool IsActive { get; set; } = true;
    [Required, StringLength(450)] public string UploadedByUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ClassSession ClassSession { get; set; } = null!;
}
