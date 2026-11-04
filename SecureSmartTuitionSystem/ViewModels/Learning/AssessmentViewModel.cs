using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Models.Learning;
namespace SecureSmartTuitionSystem.ViewModels.Learning;
public sealed class AssessmentFilter
{
    [StringLength(100)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int? ClassId { get; set; }
    [Range(1, int.MaxValue)] public int? StudentId { get; set; }
    [Range(1, int.MaxValue)] public int? SubjectId { get; set; }
    [EnumDataType(typeof(AssessmentKind))] public AssessmentKind? Kind { get; set; }
    [RegularExpression(@"^(20\d{2}|2100)-(0[1-9]|1[0-2])$")] public string? Month { get; set; }
    [Required, RegularExpression("^(active|inactive|draft|all)$")] public string Status { get; set; } = "active";
    [Required, RegularExpression("^(due_asc|due_desc|title_asc|title_desc|updated_desc)$")] public string Sort { get; set; } = "due_asc";
    [Range(1, 100000)] public int Page { get; set; } = 1;
}
public sealed class AssessmentForm : IValidatableObject
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Please select a class."), Range(1, int.MaxValue)] public int? ClassSessionId { get; set; }
    [Required(ErrorMessage = "Please enter an assessment title."), StringLength(150)] public string Title { get; set; } = "";
    [EnumDataType(typeof(AssessmentKind))] public AssessmentKind Kind { get; set; } = AssessmentKind.Assignment;
    [StringLength(4000)] public string? Instructions { get; set; }
    [Range(typeof(decimal), "0.01", "10000")] public decimal MaxMarks { get; set; } = 100;
    [Required] public DateTime? OpensAtMyt { get; set; }
    [Required] public DateTime? DueAtMyt { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
    public List<IFormFile> Files { get; set; } = [];
    public List<int> RemovedFileIds { get; set; } = [];
    [BindNever] public List<SelectListItem> Classes { get; set; } = [];
    [BindNever] public List<AssessmentFileRow> ExistingFiles { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Title)) yield return new("Please enter an assessment title.", [nameof(Title)]);
        if (OpensAtMyt.HasValue && DueAtMyt.HasValue && DueAtMyt <= OpensAtMyt) yield return new("Due date must be after the opening time.", [nameof(DueAtMyt)]);
        if (decimal.Round(MaxMarks, 2) != MaxMarks) yield return new("Use at most two decimal places.", [nameof(MaxMarks)]);
    }
}
public sealed class AssessmentFileRow
{
    public int Id { get; set; }
    public string Name { get; set; } = ""; public int Size { get; set; }
    public string SizeLabel => Size >= 1048576 ? $"{Size / 1048576m:0.##} MB" : $"{Size / 1024m:0.#} KB";
}
public sealed class AssessmentRow
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string Title { get; set; } = ""; public AssessmentKind Kind { get; set; }
    public string ClassCode { get; set; } = ""; public string ClassName { get; set; } = "";
    public string Subject { get; set; } = ""; public string Tutor { get; set; } = "";
    public string? Instructions { get; set; }
    public decimal MaxMarks { get; set; }
    public DateTime OpensAtMyt { get; set; }
    public DateTime DueAtMyt { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ResultsPublishedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = "";
    public int Participants { get; set; }
    public int Submitted { get; set; }
    public int Graded { get; set; }
    public string State => !IsActive ? "Inactive" : !PublishedAtUtc.HasValue ? "Draft" : ResultsPublishedAtUtc.HasValue ? "Results released" : "Published";
}
public sealed class AssessmentIndexModel
{
    public AssessmentFilter Filter { get; set; } = new(); public bool IsStaff { get; set; }
    public bool IsTutor { get; set; }
    public int Total { get; set; }
    public int PageCount => Math.Max(1, (Total + 24) / 25);
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<SelectListItem> Classes { get; set; } = []; public List<AssessmentRow> Rows { get; set; } = [];
}
public sealed class AssessmentParticipantRow
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = "";
    public string StudentName { get; set; } = ""; public DateTime? SubmittedAtUtc { get; set; }
    public string? SubmissionNote { get; set; }
    public decimal? Marks { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    public string RowVersion { get; set; } = "";
    public List<AssessmentFileRow> Files { get; set; } = [];
}
public sealed class AssessmentDetailsModel
{
    public AssessmentRow Assessment { get; set; } = new(); public bool IsStaff { get; set; }
    public bool CanManage { get; set; }
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanUnsubmit { get; set; }
    public bool CanGrade { get; set; }
    public List<AssessmentFileRow> QuestionFiles { get; set; } = [];
    public List<AssessmentParticipantRow> Participants { get; set; } = [];
    public SubmissionForm Submission { get; set; } = new();
}
public sealed class SubmissionForm
{
    [StringLength(64)] public string? ParticipantRowVersion { get; set; }
    public List<int> KeptFileIds { get; set; } = [];

    public int AssessmentId { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}
public sealed class AssessmentGradeForm
{
    public int ParticipantId { get; set; }
    [Required, StringLength(64)] public string RowVersion { get; set; } = "";
    [Required(ErrorMessage = "Please enter marks."), Range(typeof(decimal), "0", "10000")] public decimal? Marks { get; set; }
    [StringLength(2000, ErrorMessage = "Feedback must be 2,000 characters or fewer.")] public string? Feedback { get; set; }
}
public sealed class AssessmentResultRow
{
    public int AssessmentId { get; set; }
    public int StudentId { get; set; }
    public string Title { get; set; } = ""; public string Subject { get; set; } = ""; public string ClassCode { get; set; } = "";
    public string StudentName { get; set; } = ""; public string StudentNumber { get; set; } = "";
    public DateTime DueAtMyt { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public decimal MaxMarks { get; set; }
    public decimal? Marks { get; set; }
    public string? Feedback { get; set; }
    public bool Released { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    public decimal? Percentage => Marks.HasValue ? decimal.Round(Marks.Value / MaxMarks * 100, 2) : null;
}
public sealed class AssessmentProgressModel
{
    public AssessmentFilter Filter { get; set; } = new(); public bool IsParent { get; set; }
    public bool IsStaff { get; set; }
    public int Total { get; set; }
    public int PageCount => Math.Max(1, (Total + 24) / 25);
    public int Submitted { get; set; }
    public int Graded { get; set; }
    public decimal? Average { get; set; }
    public List<SelectListItem> SubjectChoices { get; set; } = [];
    public List<SelectListItem> Students { get; set; } = []; public List<SelectListItem> Classes { get; set; } = [];
    public List<AssessmentResultRow> Rows { get; set; } = [];
    public List<AssessmentSubjectProgress> Subjects { get; set; } = [];
}
public sealed class AssessmentGradeModel
{
    public AssessmentRow Assessment { get; set; } = new();
    public AssessmentParticipantRow Participant { get; set; } = new();
    public AssessmentGradeForm Form { get; set; } = new();
}
public sealed class AssessmentSubjectProgress
{
    public string Subject { get; set; } = "";
    public int Completed { get; set; }
    public decimal Average { get; set; }
}
