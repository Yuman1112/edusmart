using System.ComponentModel.DataAnnotations;

using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class GuardianDetailsViewModel : IValidatableObject
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
        [StringLength(6)]
        public string? EnrollmentOtp { get; set; }
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

        [RegularExpression(@"^01[0-9]-[0-9]{7,8}$", ErrorMessage = "Please enter a valid Malaysian phone number.")]
        [StringLength(12)]
        [Display(Name = "Phone Number")]
        public string? GuardianPhoneNumber { get; set; }

        [RegularExpression(@"^[0-9]{6}-[0-9]{2}-[0-9]{4}$", ErrorMessage = "Please enter a valid IC number (000000-00-0000).")]
        [StringLength(14, ErrorMessage = "Please enter a valid IC number (000000-00-0000).")]
        [Display(Name = "IC Number")]
        public string? GuardianIdentificationNumber { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (UseExistingParentAccount)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(
                GuardianIdentificationNumber))
            {
                yield break;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    GuardianIdentificationNumber,
                    @"^[0-9]{6}-[0-9]{2}-[0-9]{4}$"))
            {
                yield break;
            }

            string? message = GuardianIcValidation.GetError(GuardianIdentificationNumber);

            if (message != null)
                yield return new ValidationResult(message, new[] { nameof(GuardianIdentificationNumber) });
        }
    }
}
