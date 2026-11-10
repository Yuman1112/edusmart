using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed class EditOwnTutorProfileViewModel
{
    [BindNever]
    public string TutorId { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(254)]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your phone number.")]
    [RegularExpression(@"^01[0-9]-[0-9]{7,8}$",
        ErrorMessage = "Please enter a valid Malaysian phone number.")]
    [StringLength(12)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = "";

}
