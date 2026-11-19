using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Tutor;

// Extra files alongside the existing primary certificate. Historical files are retained.
public sealed class QualificationRequestAttachment
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public TutorQualificationUpdateRequest Request { get; set; } = null!;
    public int? CurrentDocumentId { get; set; }
    public TutorQualificationDocument? CurrentDocument { get; set; }
    [MaxLength(180)] public string FileName { get; set; } = "";
    public byte[] Content { get; set; } = [];
}
