using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TeachingChangeInput
{
    [Required(ErrorMessage = "Please select a request type.")]
    [EnumDataType(typeof(TeachingChangeType))]
    [Display(Name = "Request Type")]
    public TeachingChangeType? Type { get; set; }

    [Required(ErrorMessage = "Please select an academic level.")]
    [StringLength(20)]
    [Display(Name = "Academic Level")]
    public string AcademicLevel { get; set; } = "";

    [Required(ErrorMessage = "Please select a subject.")]
    [StringLength(10)]
    [Display(Name = "Subject")]
    public string SubjectCode { get; set; } = "";

    [Required(ErrorMessage = "Please explain why you are requesting this change.")]
    [StringLength(1000)]
    [Display(Name = "Reason for Request")]
    public string Reason { get; set; } = "";

    [Display(Name = "Supporting Evidence")]
    public IFormFile? Evidence { get; set; }

    [BindNever]
    public IReadOnlyList<TeachingChangeRow> Requests { get; set; } = [];

    [BindNever]
    public IReadOnlyList<string> ApprovedSubjects { get; set; } = [];
}

public sealed class TeachingDecisionInput
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please enter your review notes.")]
    [StringLength(1000)]
    [Display(Name = "Admin Review Notes")]
    public string ReviewNotes { get; set; } = "";

    [Display(Name = "I have reviewed the qualification or supporting evidence.")]
    public bool QualificationVerified { get; set; }

    [BindNever]
    public TeachingChangeRow? Request { get; set; }
}

public sealed class TeachingChangeRow
{
    public int Id { get; init; }
    public string TutorId { get; init; } = "";
    public string TutorName { get; init; } = "";
    public TeachingChangeType Type { get; init; }
    public TeachingChangeStatus Status { get; init; }
    public string AcademicLevel { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public string? ReviewNotes { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public int? EvidenceId { get; init; }
    public string? EvidenceName { get; init; }

    public string TypeLabel => Type switch
    {
        TeachingChangeType.AddSubject => "Add New Subject",
        TeachingChangeType.AddLevel => "Add New Level",
        TeachingChangeType.Remove => "Remove Subject / Level",
        _ => "Unknown"
    };

    public string StatusLabel => Status == TeachingChangeStatus.Pending ? "Pending Review" : Status.ToString();
    public string SubjectLabel => TeachingSubjectCatalog.GetSubjectDisplayName(SubjectCode);
}
