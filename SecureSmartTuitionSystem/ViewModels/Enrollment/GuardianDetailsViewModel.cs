using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class GuardianDetailsViewModel
    {
        // ==========================================
        // Existing Parent / Guardian Account
        // ==========================================
        [Display(Name = "I already have an EduSmart parent / guardian account")]
        public bool UseExistingParentAccount { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Email on File")]
        public string? ExistingParentEmail { get; set; }

        [RegularExpression(@"^[0-9]{4}$", ErrorMessage = "Please enter exactly four digits.")]
        [Display(Name = "Last 4 Digits of Identification Number")]
        public string? ExistingParentIdentificationLast4 { get; set; }

        // Used after successful verification.
        public bool ExistingParentVerified { get; set; }
        public string? VerifiedParentName { get; set; }
        public string? VerifiedParentEmail { get; set; }

        // ==========================================
        // New Parent / Guardian
        // ==========================================
        [StringLength(100)]
        [Display(Name = "Parent / Guardian Full Name")]
        public string? GuardianName { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Email Address")]
        public string? GuardianEmail { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(30)]
        [Display(Name = "Phone Number")]
        public string? GuardianPhoneNumber { get; set; }

        [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "Please enter the IC number in the format 000000-00-0000.")]
        [StringLength(14)]
        [Display(Name = "Identification Number")]
        public string? GuardianIdentificationNumber { get; set; }
    }
}