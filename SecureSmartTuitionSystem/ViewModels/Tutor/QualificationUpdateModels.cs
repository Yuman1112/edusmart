using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Models.Tutor;
namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class QualificationUpdateInput
{
    [Required, StringLength(200), Display(Name = "Highest Qualification")]
    public string Qualification { get; set; } = "";
    [Required, StringLength(200), Display(Name = "Field of Study")]
    public string FieldOfStudy { get; set; } = "";
    [Required, StringLength(200), Display(Name = "Area of Expertise")]
    public string AreaOfExpertise { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
    [Required, Display(Name = "Qualification Certificate")] public IFormFile? Certificate { get; set; }
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
    public string CertificateName { get; set; } = "";
    public string Reason { get; set; } = "";
    public QualificationUpdateStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public string StatusLabel => Status == QualificationUpdateStatus.Pending ? "Pending Review" : Status.ToString();
}

public sealed class QualificationDecisionInput
{
    public int Id { get; set; }
    [Required, StringLength(1000), Display(Name = "Review Notes")] public string ReviewNotes { get; set; } = "";
    [Display(Name = "I have reviewed the certificate and proposed qualifications.")]
    public bool QualificationVerified { get; set; }
    [BindNever] public QualificationUpdateRow? Request { get; set; }
    [BindNever] public string CurrentQualification { get; set; } = "";
    [BindNever] public string CurrentFieldOfStudy { get; set; } = "";
    [BindNever] public string CurrentAreaOfExpertise { get; set; } = "";
}
