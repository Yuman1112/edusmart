using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class EditTutorViewModel
{
    [Required]
    public string UserId { get; set; } = "";

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
    public string Qualification { get; set; } = "";

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