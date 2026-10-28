
using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class CreateTutorViewModel
{
    [Required(ErrorMessage = "Please enter the tutor's full name.")]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter the tutor's email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter the tutor's phone number.")]
    [RegularExpression(
        @"^01[0-9]-[0-9]{7,8}$",
        ErrorMessage = "Please enter a valid Malaysian phone number.")]
    [StringLength(12)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the tutor's qualification.")]
    [StringLength(200)]
    public string Qualification { get; set; } = "";

    [EnumDataType(typeof(SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType), ErrorMessage = "Please select a valid employment type.")]
    [Display(Name = "Employment Type")]
    public SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType EmploymentType { get; set; }
        = SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.PartTime;

    [StringLength(500)]
    [Display(Name = "Teaching Experience")]
    public string? TeachingExperience { get; set; }

    // Optional: Additional teaching expertise.
    [StringLength(200)]
    [Display(Name = "Area of Expertise")]
    public string? AreaOfExpertise { get; set; }

    // A tutor can teach multiple academic levels and subjects.
    [Display(Name = "Teaching Subjects & Levels")]
    public List<TutorTeachingLevelInput> TeachingLevels { get; set; }
        = new();
}

// Represents one academic level and its selected subjects.
public sealed class TutorTeachingLevelInput
{
    [Required(ErrorMessage = "Please select an education stage.")]
    [StringLength(20)]
    public string EducationStage { get; set; } = "";

    [Required(ErrorMessage = "Please select an academic level.")]
    [StringLength(20)]
    public string AcademicLevel { get; set; } = "";

    public List<string> SubjectCodes { get; set; } = new();
}

public sealed class TutorListItem
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";

    public string AreaOfExpertise { get; set; } = "";

    public bool IsActive { get; set; }
    public bool SetupPending { get; set; }
}
