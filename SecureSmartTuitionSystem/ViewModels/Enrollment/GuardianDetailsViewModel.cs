using System.ComponentModel.DataAnnotations;

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

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            var yy = int.Parse(GuardianIdentificationNumber[..2], System.Globalization.CultureInfo.InvariantCulture);
            var year = yy <= today.Year % 100 ? 2000 + yy : 1900 + yy;
            var dateText = $"{year:D4}{GuardianIdentificationNumber[2..6]}";

            string? message = null;
            if (!DateOnly.TryParseExact(dateText, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var birthDate))
                message = "The IC number contains an invalid date of birth.";
            else if (birthDate >= today)
                message = "The date of birth must be earlier than today.";

            if (message != null)
                yield return new ValidationResult(message, new[] { nameof(GuardianIdentificationNumber) });
        }
    }
}
