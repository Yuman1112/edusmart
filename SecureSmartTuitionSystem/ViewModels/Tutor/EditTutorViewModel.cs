using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class EditTutorViewModel
{
    [Required]
    public string UserId { get; set; } = "";

    [Required]
    public long? ProfileVersion { get; set; }

    [BindNever]
    public string TutorId { get; set; } = "";

    [Required(ErrorMessage = "Please enter the tutor's email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(
        254,
        ErrorMessage = "The email address cannot exceed 254 characters.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = "";

    [BindNever]
    public string CurrentEmail { get; set; } = "";

    [BindNever]
    public string? PendingEmail { get; set; }

    [BindNever]
    public Guid? PendingEmailRequestId { get; set; }

    [BindNever]
    public DateTime? PendingEmailExpiresAtUtc { get; set; }

    [Required(ErrorMessage = "Please enter the tutor's full name.")]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter the tutor's phone number.")]
    [RegularExpression(
        @"^01[0-9]-[0-9]{7,8}$",
        ErrorMessage = "Please enter a valid Malaysian phone number.")]
    [StringLength(12)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "Please enter the tutor's qualification.")]
    [StringLength(200)]
    [Display(Name = "Highest Qualification")]
    public string Qualification { get; set; } = "";

    [StringLength(200)]
    [Display(Name = "Field of Study")]
    public string? FieldOfStudy { get; set; }

    [Display(Name = "Qualification Certificate")]
    public IFormFile? Certificate { get; set; }

    [Display(Name = "Resume / CV")]
    public IFormFile? Resume { get; set; }

    [Display(Name = "I have verified the tutor's qualifications for the selected subjects and levels.")]
    public bool QualificationVerified { get; set; }

    [BindNever]
    public IReadOnlyList<TutorDocumentSummary> Documents { get; set; } = [];

    [EnumDataType(typeof(SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType), ErrorMessage = "Please select a valid employment type.")]
    [Display(Name = "Employment Type")]
    public SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType EmploymentType { get; set; }
        = SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.PartTime;

    [StringLength(200)]
    [Display(Name = "Area of Expertise")]
    public string? AreaOfExpertise { get; set; }

    [StringLength(500)]
    [Display(Name = "Teaching Experience")]
    public string? TeachingExperience { get; set; }

    [Display(Name = "Teaching Subjects & Levels")]
    public List<TutorTeachingLevelInput> TeachingLevels { get; set; }
        = new();
}

public sealed record TutorDocumentSummary(
    int Id, SecureSmartTuitionSystem.Models.Tutor.TutorDocumentKind Kind, string FileName);
