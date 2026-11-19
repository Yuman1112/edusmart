using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Models.Tutor;
namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class QualificationUpdateInput
{
    [Required, StringLength(200), Display(Name = "Highest Qualification")]
    public string Qualification { get; set; } = "";
    [StringLength(200), Display(Name = "Field of Study")]
    public string? FieldOfStudy { get; set; }
    [StringLength(200), Display(Name = "Area of Expertise")]
    public string? AreaOfExpertise { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
    [Display(Name = "Qualification Certificate")] public IFormFile? Certificate { get; set; }
    [Display(Name = "Supporting Documents")] public List<IFormFile> Certificates { get; set; } = [];
    public string? BaselineToken { get; set; }
    [BindNever] public string CurrentQualification { get; set; } = "";
    [BindNever] public string? CurrentFieldOfStudy { get; set; }
    [BindNever] public string? CurrentAreaOfExpertise { get; set; }
    [BindNever] public bool CurrentVerified { get; set; }
    [BindNever] public IReadOnlyList<QualificationUpdateRow> Requests { get; set; } = [];
}

public sealed class QualificationUpdateRow
{
    public int Id { get; set; }
    public string TutorId { get; set; } = "";
    public string TutorName { get; set; } = "";
    public string Qualification { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string AreaOfExpertise { get; set; } = "";
    public string OriginalQualification { get; set; } = "";
    public string? OriginalFieldOfStudy { get; set; }
    public string? OriginalAreaOfExpertise { get; set; }
    public string CertificateName { get; set; } = "";
    public string Reason { get; set; } = "";
    public QualificationUpdateStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public long CertificateSize { get; set; }
    public List<QualificationFileSummary> AdditionalFiles { get; set; } = [];
    public IReadOnlyList<QualificationFileSummary> Files => (string.IsNullOrWhiteSpace(CertificateName) ? Array.Empty<QualificationFileSummary>() : new[]{new QualificationFileSummary(0, CertificateName, CertificateSize)}).Concat(AdditionalFiles).ToArray();
    public bool HasCertificate => !string.IsNullOrWhiteSpace(CertificateName);
    public IReadOnlyList<QualificationFieldChange> Changes => new[] {
        new QualificationFieldChange("Highest Qualification", OriginalQualification, Qualification),
        new QualificationFieldChange("Field of Study", OriginalFieldOfStudy, FieldOfStudy),
        new QualificationFieldChange("Area of Expertise", OriginalAreaOfExpertise, AreaOfExpertise)
    }.Where(c => !SecureSmartTuitionSystem.Services.Tutor.QualificationChangeRules.Same(c.Before, c.After)).ToArray();
    public IReadOnlyList<string> ChangedItems => Changes.Select(c => c.Label).Concat(HasCertificate ? new[]{"Supporting Documents"} : Array.Empty<string>()).ToArray();
    public string StatusLabel => Status == QualificationUpdateStatus.Pending ? "Pending Review" : Status.ToString();
}

public sealed class QualificationDecisionInput
{
    public int Id { get; set; }
    [Required, StringLength(1000), Display(Name = "Review Notes")] public string ReviewNotes { get; set; } = "";
    [Display(Name = "I have reviewed the requested changes and any supporting evidence.")]
    public bool QualificationVerified { get; set; }
    [BindNever] public TutorDocumentSummary? CurrentCertificate { get; set; }
    [BindNever] public QualificationUpdateRow? Request { get; set; }
    [BindNever] public string CurrentQualification { get; set; } = "";
    [BindNever] public string CurrentFieldOfStudy { get; set; } = "";
    [BindNever] public string CurrentAreaOfExpertise { get; set; } = "";
}

public sealed class QualificationLiveProfile
{
    public string Qualification { get; set; } = "";
    public string? FieldOfStudy { get; set; }
    public string? AreaOfExpertise { get; set; }
    public bool Verified { get; set; }
    public QualificationUpdateRow? Pending { get; set; }
    public IReadOnlyList<TutorDocumentSummary> Documents { get; set; } = [];
}
public sealed class QualificationLiveState
{
    public IReadOnlyList<QualificationUpdateRow> Requests { get; set; } = [];
    public QualificationLiveProfile? Profile { get; set; }
}

public sealed record QualificationFieldChange(string Label, string? Before, string? After);

public sealed record QualificationFileSummary(int Id, string Name, long Size);
public sealed class QualificationFileList
{
    public int OwnerId { get; set; }
    public bool Current { get; set; }
    public IReadOnlyList<QualificationFileSummary> Files { get; set; } = [];
}
