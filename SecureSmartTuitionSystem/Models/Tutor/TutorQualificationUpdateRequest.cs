using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Tutor;

public enum QualificationUpdateStatus { Pending, Approved, Rejected, Cancelled }

public sealed class TutorQualificationUpdateRequest
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    [MaxLength(200)] public string Qualification { get; set; } = "";
    [MaxLength(200)] public string FieldOfStudy { get; set; } = "";
    [MaxLength(200)] public string AreaOfExpertise { get; set; } = "";
    [MaxLength(200)] public string OriginalQualification { get; set; } = "";
    [MaxLength(200)] public string? OriginalFieldOfStudy { get; set; }
    [MaxLength(200)] public string? OriginalAreaOfExpertise { get; set; }
    public DateTime? OriginalVerifiedAtUtc { get; set; }
    [MaxLength(180)] public string CertificateName { get; set; } = "";
    public byte[] CertificateContent { get; set; } = [];
    [MaxLength(1000)] public string Reason { get; set; } = "";
    public QualificationUpdateStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    [MaxLength(1000)] public string? ReviewNotes { get; set; }
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}
