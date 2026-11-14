using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Tutor;

public enum TeachingChangeType { AddSubject = 1, AddLevel, Remove }
public enum TeachingChangeStatus { Pending = 0, Approved, Rejected }

public sealed class TutorTeachingChangeRequest
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public int? EvidenceRequestId { get; set; }
    public TutorTeachingChangeRequest? EvidenceRequest { get; set; }
    public TeachingChangeType Type { get; set; }
    public TeachingChangeStatus Status { get; set; }

    [MaxLength(20)]
    public string AcademicLevel { get; set; } = "";

    [MaxLength(10)]
    public string SubjectCode { get; set; } = "";

    [MaxLength(1000)]
    public string Reason { get; set; } = "";

    public DateTime SubmittedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }
}

public enum TutorDocumentKind { Certificate = 1, Resume, RequestEvidence }

public sealed class TutorQualificationDocument
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public int? TeachingChangeRequestId { get; set; }
    public TutorTeachingChangeRequest? TeachingChangeRequest { get; set; }
    public TutorDocumentKind Kind { get; set; }

    [MaxLength(180)]
    public string FileName { get; set; } = "";

    public byte[] Content { get; set; } = [];
}
