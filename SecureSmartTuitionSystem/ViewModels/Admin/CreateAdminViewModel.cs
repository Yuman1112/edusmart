
using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Admin
{
    public sealed class CreateAdminViewModel
    {
        [Required(ErrorMessage = "Please enter the full name.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter an email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(254)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(30)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Position")]
        public string? Position { get; set; }
    }
}
