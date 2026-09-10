using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class StudentDetailsViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Please enter the student's full name.")]
        [StringLength(100)]
        [Display(Name = "Student's Full Name")]
        public string StudentName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select an identification type.")]
        [Display(Name = "Identification Type")]
        public string StudentIdentificationType { get; set; } = "IC";

        [Required(ErrorMessage = "Please enter the student's identification number.")]
        [StringLength(
            30,
            ErrorMessage = "The identification number cannot exceed 30 characters.")]
        [Display(Name = "Identification Number")]
        public string StudentIdentificationNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select the student's date of birth.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? StudentDateOfBirth { get; set; }

        [Required(ErrorMessage = "Please select a gender.")]
        [Display(Name = "Gender")]
        public string StudentGender { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the student's current school.")]
        [StringLength(150)]
        [Display(Name = "Current School")]
        public string StudentSchool { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (StudentDateOfBirth.HasValue &&
                StudentDateOfBirth.Value.Date >= DateTime.Today)
            {
                yield return new ValidationResult(
                    "The date of birth must be earlier than today.",
                    new[] { nameof(StudentDateOfBirth) });
            }

            if (!string.IsNullOrWhiteSpace(StudentGender) &&
                StudentGender != "Male" &&
                StudentGender != "Female")
            {
                yield return new ValidationResult(
                    "Please select a valid gender.",
                    new[] { nameof(StudentGender) });
            }

            if (!string.IsNullOrWhiteSpace(StudentIdentificationType) &&
                StudentIdentificationType != "IC" &&
                StudentIdentificationType != "BirthCertificate")
            {
                yield return new ValidationResult(
                    "Please select a valid identification type.",
                    new[] { nameof(StudentIdentificationType) });
            }

            if (StudentIdentificationType == "IC" &&
                !string.IsNullOrWhiteSpace(StudentIdentificationNumber))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                        StudentIdentificationNumber,
                        @"^\d{6}-\d{2}-\d{4}$"))
                {
                    yield return new ValidationResult(
                        "Please enter the IC number in the format 000000-00-0000.",
                        new[] { nameof(StudentIdentificationNumber) });
                }
            }

            if (StudentIdentificationType == "BirthCertificate" &&
                !string.IsNullOrWhiteSpace(StudentIdentificationNumber))
            {
                var birthCertificatePattern =
                    @"^[A-Za-z0-9][A-Za-z0-9\s./-]{2,19}$";

                if (!System.Text.RegularExpressions.Regex.IsMatch(
                        StudentIdentificationNumber.Trim(),
                        birthCertificatePattern))
                {
                    yield return new ValidationResult(
                        "Please enter a valid birth certificate number using letters, numbers, spaces, '/', '-' or '.'.",
                        new[] { nameof(StudentIdentificationNumber) });
                }
            }
        }
    }
}