using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class StudentDetailsViewModel : IValidatableObject
    {
        [Required(
            ErrorMessage = "Please enter the student's full name.")]
        [StringLength(100)]
        [Display(Name = "Student's Full Name")]
        public string StudentName { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Please select an identification type.")]
        [Display(Name = "Identification Type")]
        public string StudentIdentificationType { get; set; } = "IC";


        [Required(
            ErrorMessage = "Please enter the student's identification number.")]
        [StringLength(
            30,
            ErrorMessage =
                "The identification number cannot exceed 30 characters.")]
        [Display(Name = "Identification Number")]
        public string StudentIdentificationNumber { get; set; }
            = string.Empty;


        [Required(
            ErrorMessage = "Please select the student's date of birth.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? StudentDateOfBirth { get; set; }


        [Required(
            ErrorMessage = "Please select a gender.")]
        [Display(Name = "Gender")]
        public string StudentGender { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Please enter the student's current school.")]
        [StringLength(150)]
        [Display(Name = "Current School")]
        public string StudentSchool { get; set; } = string.Empty;


        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            // ==========================================
            // DATE OF BIRTH
            // ==========================================
            if (StudentDateOfBirth.HasValue &&
                StudentDateOfBirth.Value.Date < DateTime.Today)
            {
                var age =
                    CalculateAge(
                        StudentDateOfBirth.Value.Date);

                if (age < 5 || age > 19)
                {
                    yield return new ValidationResult(
                        "The student's age is not within the supported range for Year 1 to Form 5.",
                        new[]
                        {
                            nameof(StudentDateOfBirth)
                        });
                }
            }


            // ==========================================
            // GENDER
            // ==========================================
            if (!string.IsNullOrWhiteSpace(StudentGender) &&
                StudentGender != "Male" &&
                StudentGender != "Female")
            {
                yield return new ValidationResult(
                    "Please select a valid gender.",
                    new[]
                    {
                        nameof(StudentGender)
                    });
            }


            // ==========================================
            // IDENTIFICATION TYPE
            // ==========================================
            if (!string.IsNullOrWhiteSpace(
                    StudentIdentificationType) &&
                StudentIdentificationType != "IC" &&
                StudentIdentificationType !=
                    "BirthCertificate")
            {
                yield return new ValidationResult(
                    "Please select a valid identification type.",
                    new[]
                    {
                        nameof(StudentIdentificationType)
                    });
            }


            // ==========================================
            // IC VALIDATION
            // ==========================================
            if (StudentIdentificationType == "IC" &&
                !string.IsNullOrWhiteSpace(
                    StudentIdentificationNumber))
            {
                if (!Regex.IsMatch(
                        StudentIdentificationNumber,
                        @"^\d{6}-\d{2}-\d{4}$"))
                {
                    yield return new ValidationResult(
                        "Please enter the IC number in the format 000000-00-0000.",
                        new[]
                        {
                            nameof(
                                StudentIdentificationNumber)
                        });

                    yield break;
                }


                var digits =
                    new string(
                        StudentIdentificationNumber
                            .Where(char.IsDigit)
                            .ToArray());


                var yy =
                    int.Parse(
                        digits.Substring(0, 2));

                var mm =
                    int.Parse(
                        digits.Substring(2, 2));

                var dd =
                    int.Parse(
                        digits.Substring(4, 2));


                var currentTwoDigitYear =
                    DateTime.Today.Year % 100;

                var fullYear =
                    yy <= currentTwoDigitYear
                        ? 2000 + yy
                        : 1900 + yy;


                DateTime? icDateOfBirth = null;

                try
                {
                    icDateOfBirth =
                        new DateTime(
                            fullYear,
                            mm,
                            dd);
                }
                catch
                {
                    icDateOfBirth = null;
                }


                if (!icDateOfBirth.HasValue)
                {
                    yield return new ValidationResult(
                        "The IC number contains an invalid date of birth.",
                        new[]
                        {
                            nameof(
                                StudentIdentificationNumber)
                        });

                    yield break;
                }


                if (StudentDateOfBirth.HasValue &&
                    StudentDateOfBirth.Value.Date !=
                    icDateOfBirth.Value.Date)
                {
                    yield return new ValidationResult(
                        "The date of birth does not match the IC number.",
                        new[]
                        {
                            nameof(StudentDateOfBirth)
                        });
                }


                var lastDigit =
                    int.Parse(
                        digits.Substring(11, 1));

                var expectedGender =
                    lastDigit % 2 == 0
                        ? "Female"
                        : "Male";


                if (!string.IsNullOrWhiteSpace(
                        StudentGender) &&
                    StudentGender != expectedGender)
                {
                    yield return new ValidationResult(
                        "The gender does not match the IC number.",
                        new[]
                        {
                            nameof(StudentGender)
                        });
                }
            }


            // ==========================================
            // BIRTH CERTIFICATE VALIDATION
            // ==========================================
            if (StudentIdentificationType ==
                    "BirthCertificate" &&
                !string.IsNullOrWhiteSpace(
                    StudentIdentificationNumber))
            {
                var birthCertificatePattern =
                    @"^[A-Za-z0-9][A-Za-z0-9\s./-]{2,19}$";


                if (!Regex.IsMatch(
                        StudentIdentificationNumber.Trim(),
                        birthCertificatePattern))
                {
                    yield return new ValidationResult(
                        "Please enter a valid birth certificate number using letters, numbers, spaces, '/', '-' or '.'.",
                        new[]
                        {
                            nameof(
                                StudentIdentificationNumber)
                        });
                }
            }
        }

        private static int CalculateAge(
            DateTime dateOfBirth)
        {
            var today = DateTime.Today;

            var age =
                today.Year - dateOfBirth.Year;

            if (dateOfBirth.Date >
                today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

    }
}