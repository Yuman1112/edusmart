using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public class TutorExportFilterViewModel
{
    [StringLength(1000)] public string? Search { get; set; }
    [RegularExpression("^(active|inactive)$")] public string? Status { get; set; }
    [RegularExpression("^(pending|completed)$")] public string? Setup { get; set; }
    [Required, RegularExpression("^(xlsx|csv)$")]
    public string Format { get; set; } = "xlsx";
}

public sealed class TutorExportViewModel : TutorExportFilterViewModel
{
    [BindNever] public int TotalRecords { get; set; }
    [BindNever] public List<TutorExportRowViewModel> PreviewRows { get; set; } = new();
}

public sealed class TutorExportRowViewModel
{
    public string TutorId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string AreaOfExpertise { get; set; } = string.Empty;
    public string TeachingExperience { get; set; } = string.Empty;
    public string TeachingSummary { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool SetupPending { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<TutorExportSubjectViewModel> TeachingSubjects { get; set; } = new();
}

public sealed class TutorExportSubjectViewModel
{
    public string Stage { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}