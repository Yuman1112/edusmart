using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class EnrollmentSelectionViewModel :
        IValidatableObject
    {
        /*
         * Applicant summary
         */
        public string StudentName { get; set; }
            = string.Empty;

        public string GuardianName { get; set; }
            = string.Empty;

        /*
         * Client-side class filters
         */
        public string? EducationStage { get; set; }

        public string? AcademicLevel { get; set; }

        public string? SubjectName { get; set; }

        /*
         * Selected classes
         */
        public List<int> SelectedClassSessionIds { get; set; }
            = new();

        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public List<EnrollmentScheduleConflictViewModel> ScheduleConflicts { get; set; } = new();

        public List<EnrollmentSelectableClassViewModel>
            AvailableClasses
        { get; set; }
            = new();

        /*
         * Enrollment information
         */
        [Required(
            ErrorMessage =
                "Please select a requested start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Requested Start Date")]
        public DateTime? RequestedStartDate { get; set; }

        [Required(
            ErrorMessage =
                "Please select an enrollment plan.")]
        [Range(
            1,
            12,
            ErrorMessage =
                "Please select a valid enrollment plan.")]
        [Display(Name = "Enrollment Plan")]
        public int EnrollmentDurationMonths { get; set; }
            = 1;

        public bool FinalTwoWeeksConfirmed { get; set; }

        public bool FinalTwoWeeksFeeAcknowledged { get; set; }

        [StringLength(
            500,
            ErrorMessage =
                "Applicant notes cannot exceed 500 characters.")]
        [Display(Name = "Applicant Notes")]
        public string? ApplicantNotes { get; set; }

        /*
         * Hardcopy and delivery information
         */
        [Display(Name = "Request Hardcopy Materials")]
        public bool HardcopyMaterialsRequested { get; set; }

        [StringLength(100)]
        [Display(Name = "Recipient Name")]
        public string? DeliveryRecipientName { get; set; }

        [StringLength(150)]
        [Display(Name = "Address Line 1")]
        public string? DeliveryAddressLine1 { get; set; }

        [StringLength(150)]
        [Display(Name = "Address Line 2")]
        public string? DeliveryAddressLine2 { get; set; }

        [StringLength(80)]
        [Display(Name = "City")]
        public string? DeliveryCity { get; set; }

        [StringLength(80)]
        [Display(Name = "State")]
        public string? DeliveryState { get; set; }

        [RegularExpression(
            @"^\d{5}$",
            ErrorMessage =
                "Postcode must contain exactly 5 digits.")]
        [Display(Name = "Postcode")]
        public string? DeliveryPostcode { get; set; }

        [Display(Name = "Delivery Region")]
        public string? DeliveryRegion { get; set; }

        /*
         * Display-only fee information.
         *
         * The Controller must calculate these values again
         * before saving. Never trust totals submitted by
         * JavaScript.
         */
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public DateTime? SelectedTermEndDate { get; set; }

        public int SelectedClassCount { get; set; }
        public List<string> SelectedClassNames { get; set; } = new();

        public int UniqueSubjectCount { get; set; }

        public decimal MonthlyTuitionFee { get; set; }

        public decimal TuitionSubtotal { get; set; }

        public decimal DiscountRate { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal DiscountedTuition { get; set; }

        public decimal HardcopyFee { get; set; }

        public decimal ShippingFee { get; set; }

        public decimal EstimatedTotal { get; set; }

        public bool IsRequestedStartDateInFinalTwoWeeks
        {
            get;
            set;
        }

        /*
         * Dropdown options
         */
        public List<string> AcademicLevelOptions { get; set; }
            = new();

        public List<string> SubjectOptions { get; set; }
            = new();

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (SelectedClassSessionIds == null ||
                SelectedClassSessionIds.Count == 0)
            {
                yield return new ValidationResult(
                    "Please select at least one class.",
                    new[]
                    {
                        nameof(SelectedClassSessionIds)
                    });
            }

            if (RequestedStartDate.HasValue)
            {
                DateTime selectedDate =
                    RequestedStartDate.Value.Date;

                if (selectedDate < SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService.Today)
                {
                    yield return new ValidationResult(
                        "Requested start date cannot be earlier than today.",
                        new[]
                        {
                            nameof(RequestedStartDate)
                        });
                }

                if (selectedDate >
                    SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService.Today.AddYears(1))
                {
                    yield return new ValidationResult(
                        "Requested start date cannot be more than one year from today.",
                        new[]
                        {
                            nameof(RequestedStartDate)
                        });
                }

                // Class-specific confirmation is validated against trusted database records
                // by EnrollmentSubmissionController, never against posted AvailableClasses.

            }

            int[] validDurationOptions =
            {
                1,
                2,
                3,
                6,
                9,
                12
            };

            if (!validDurationOptions.Contains(
                    EnrollmentDurationMonths))
            {
                yield return new ValidationResult(
                    "Please select a valid enrollment plan.",
                    new[]
                    {
                        nameof(EnrollmentDurationMonths)
                    });
            }

            if (!HardcopyMaterialsRequested)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(
                    DeliveryRecipientName))
            {
                yield return new ValidationResult(
                    "Recipient name is required when hardcopy materials are requested.",
                    new[]
                    {
                        nameof(DeliveryRecipientName)
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    DeliveryAddressLine1))
            {
                yield return new ValidationResult(
                    "Delivery address is required when hardcopy materials are requested.",
                    new[]
                    {
                        nameof(DeliveryAddressLine1)
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    DeliveryCity))
            {
                yield return new ValidationResult(
                    "City is required when hardcopy materials are requested.",
                    new[]
                    {
                        nameof(DeliveryCity)
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    DeliveryState))
            {
                yield return new ValidationResult(
                    "State is required when hardcopy materials are requested.",
                    new[]
                    {
                        nameof(DeliveryState)
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    DeliveryPostcode))
            {
                yield return new ValidationResult(
                    "Postcode is required when hardcopy materials are requested.",
                    new[]
                    {
                        nameof(DeliveryPostcode)
                    });
            }

        }
    }

    public class EnrollmentSelectableClassViewModel
    {
        public int ClassSessionId { get; set; }
        public DateTime TermStart { get; set; }
        public DateTime TermEnd { get; set; }


        public List<string> CancelledDates { get; set; } = new();

        public string ClassCode { get; set; }
            = string.Empty;

        public string ClassName { get => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(_className); set => _className = value; }
        private string _className = "";

        public string EducationStage { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string SubjectName { get; set; }
            = string.Empty;

        public string TutorName { get; set; }
            = string.Empty;

        public string Day { get; set; }
            = string.Empty;

        public string StartTime { get; set; }
            = string.Empty;

        public string EndTime { get; set; }
            = string.Empty;

        public decimal MonthlyFee { get; set; }

        public int Capacity { get; set; }

        public Dictionary<string,int> AvailabilityByDate { get; set; } = new();
        public int StudentCount { get; set; }

        public int RemainingCapacity =>
            Math.Max(0, Capacity - StudentCount);

        public ClassSessionStatus Status { get; set; }
    }

    public class EnrollmentSubmittedViewModel
    {
        public int EnrollmentApplicationId { get; set; }

        public string ApplicationNumber { get; set; }
            = string.Empty;

        public string StudentName { get; set; }
            = string.Empty;

        public string GuardianName { get; set; }
            = string.Empty;

        public string GuardianEmail { get; set; }
            = string.Empty;

        public int SelectedClassCount { get; set; }
        public List<string> SelectedClassNames { get; set; } = new();

        public decimal TotalMonthlyFee { get; set; }

        public DateTime RequestedStartDate { get; set; }

        public int EnrollmentDurationMonths { get; set; }

        public decimal DiscountRate { get; set; }

        public decimal EstimatedTotal { get; set; }

        public EnrollmentStatus Status { get; set; }

        public DateTime SubmittedAt { get; set; }
    }
    public sealed class EnrollmentScheduleConflictViewModel
    {
        public string PairTitle { get; set; } = string.Empty;
        public string FirstClassName { get; set; } = string.Empty;
        public string SecondClassName { get; set; } = string.Empty;
        public DayOfWeek Day { get; set; }
        public TimeOnly FirstStart { get; set; }
        public TimeOnly FirstEnd { get; set; }
        public TimeOnly SecondStart { get; set; }
        public TimeOnly SecondEnd { get; set; }
        public TimeOnly OverlapStart { get; set; }
        public TimeOnly OverlapEnd { get; set; }
    }

}
