using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Learning;

public sealed class LearningMaterialAttachment
{
    public int LearningMaterialAttachmentId { get; set; }
    public int LearningMaterialId { get; set; }
    [Required, StringLength(180)] public string OriginalFileName { get; set; } = "";
    [Required, StringLength(100)] public string ContentType { get; set; } = "";
    public int FileSize { get; set; }
    [Required] public byte[] FileContent { get; set; } = [];
    public LearningMaterial LearningMaterial { get; set; } = null!;
}
