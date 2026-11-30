using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace SecureSmartTuitionSystem.ViewModels.Learning;

public sealed class MaterialFilter
{
    public int? ClassId { get; set; }
    [StringLength(100)] public string? Search { get; set; }
    public string Status { get; set; } = "active";
    [Required, RegularExpression("^(updated|title|class|subject|status)_(asc|desc)$")]
    public string Sort { get; set; } = "updated_desc";
    [Range(1, 100000)] public int Page { get; set; } = 1;
}
public sealed class MaterialAttachmentRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Size { get; set; }
    public string SizeLabel => Size >= 1048576 ? $"{Size / 1048576m:0.##} MB" : $"{Size / 1024m:0.#} KB";
}
public sealed class MaterialRow
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int FileCount { get; set; } = 1;
    public List<MaterialAttachmentRow> Attachments { get; set; } = [];
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string ClassCode { get; set; } = "";
    public string ClassName { get => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(_className); set => _className = value; }
        private string _className = "";
    public string Subject { get; set; } = "";
    public string Level { get; set; } = "";
    public string Tutor { get; set; } = "";
    public string FileName { get; set; } = "";
    public int Size { get; set; }
    public bool IsActive { get; set; }
    public bool CanManage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string RowVersion { get; set; } = "";
    public string SizeLabel => Size >= 1048576 ? $"{Size / 1048576m:0.##} MB" : $"{Size / 1024m:0.#} KB";
}
public sealed class MaterialIndexModel
{
    public MaterialFilter Filter { get; set; } = new();
    public bool IsTutor { get; set; }
    public bool IsStaff { get; set; }
    public int Total { get; set; }
    public int PageCount => Math.Max(1, (Total + 24) / 25);
    public List<SelectListItem> Classes { get; set; } = [];
    public List<MaterialRow> Rows { get; set; } = [];
}
public sealed class MaterialForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Please select a class."), Range(1, int.MaxValue), Display(Name = "Class")] public int? ClassSessionId { get; set; }
    [Required(ErrorMessage = "Please enter a material title."), StringLength(150)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    [Display(Name = "Material File")] public IFormFile? File { get; set; }
    [Display(Name = "Material Files")] public List<IFormFile> Files { get; set; } = [];
    public List<int> RemovedAttachmentIds { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
    [BindNever] public string? CurrentFileName { get; set; }
    [BindNever] public List<MaterialAttachmentRow> CurrentFiles { get; set; } = [];
    [BindNever] public List<SelectListItem> Classes { get; set; } = [];
}
