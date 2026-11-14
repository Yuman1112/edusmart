using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class TeachingChangeInput : IValidatableObject
{
    [Required(ErrorMessage = "Please select a request type.")]
    [EnumDataType(typeof(TeachingChangeType))]
    [Display(Name = "Request Type")]
    public TeachingChangeType? Type { get; set; } = TeachingChangeType.AddSubject;

    [MinLength(1), MaxLength(10)]
    public List<TeachingSubjectInput> Items { get; set; } = [new()];

    [Required(ErrorMessage = "Please explain why you are requesting this change.")]
    [StringLength(1000)]
    [Display(Name = "Reason for Request")]
    public string Reason { get; set; } = "";

    [Display(Name = "Confirmation")]
    public bool ConfirmAccuracy { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ConfirmAccuracy)
            yield return new ValidationResult(
                "Please confirm that you are qualified to teach the selected subjects and levels.",
                [nameof(ConfirmAccuracy)]);
    }

    [Display(Name = "Supporting Evidence")]
    public List<IFormFile>? Evidence { get; set; }

    [BindNever]
    public IReadOnlyList<string> UnavailableCombinations { get; set; } = [];

    [MaxLength(5)]
    public List<int> ReferenceDocumentIds { get; set; } = [];

    [BindNever]
    public IReadOnlyList<TeachingEvidenceRow> ExistingEvidence { get; set; } = [];

    [BindNever]
    public IReadOnlyList<TeachingChangeRow> Requests { get; set; } = [];

    [BindNever]
    public IReadOnlyList<string> ApprovedSubjects { get; set; } = [];
}

public sealed class TeachingSubjectInput
{
    [Required(ErrorMessage = "Select an academic level."), StringLength(20)]
    public string AcademicLevel { get; set; } = "";

    [Required(ErrorMessage = "Select a subject."), StringLength(10)]
    public string SubjectCode { get; set; } = "";
}

public sealed class TeachingRequestListViewModel
{
    public bool IsAdmin { get; init; }
    public IReadOnlyList<TeachingChangeRow> Requests { get; init; } = [];
    public IReadOnlyList<string> ApprovedSubjects { get; init; } = [];
    public IReadOnlyList<string> UnavailableCombinations { get; init; } = [];
}

public sealed class TeachingEvidenceRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Size { get; init; }
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
    public int SubmissionId { get; init; }
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
    public IReadOnlyList<TeachingEvidenceRow> Evidence { get; init; } = [];

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
