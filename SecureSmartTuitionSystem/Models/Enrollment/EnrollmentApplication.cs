using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public class EnrollmentApplication
    {
        public int EnrollmentApplicationId { get; set; }

        [Required]
        [RegularExpression(
            @"^EA\d{5}$",
            ErrorMessage =
                "Application number must follow EA00001 format.")]
        [StringLength(7)]
        public string ApplicationNumber { get; set; }
            = string.Empty;

        /*
         * Nullable because a prospective applicant can
         * submit an enrollment application without logging in.
         */
        public string? SubmittedByUserId { get; set; }

        public int? ExistingParentProfileId { get; set; }

        public int? ExistingStudentProfileId { get; set; }

        /*
         * Guardian snapshot
         */
        [Required]
        [StringLength(100)]
        public string GuardianName { get; set; }
            = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string GuardianEmail { get; set; }
            = string.Empty;

        [Required]
        [Phone]
        [StringLength(30)]
        public string GuardianPhoneNumber { get; set; }
            = string.Empty;

        [StringLength(50)]
        public string? GuardianIdentificationNumber
        {
            get;
            set;
        }

        [StringLength(4, MinimumLength = 4)]
        public string? GuardianIdentificationLast4
        {
            get;
            set;
        }

        /*
         * Student snapshot
         */
        [Required]
        [StringLength(100)]
        public string StudentName { get; set; }
            = string.Empty;

        [Required]
        [StringLength(14, ErrorMessage = "The IC number cannot exceed 14 characters.")]
        public string StudentIdentificationNumber { get; set; }
            = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime StudentDateOfBirth { get; set; }

        [StringLength(20)]
        public string? StudentGender { get; set; }

        [StringLength(150)]
        public string? StudentSchool { get; set; }

        [Required]
        [StringLength(30)]
        public string AcademicLevel { get; set; }
            = string.Empty;

        /*
         * Enrollment information
         */
        [Required]
        [DataType(DataType.Date)]
        public DateTime RequestedStartDate { get; set; }

        [Range(1, 12)]
        public int EnrollmentDurationMonths { get; set; }
            = 1;

        public bool FinalTwoWeeksConfirmed { get; set; }

        /*
         * Hardcopy and delivery information
         */
        public bool HardcopyMaterialsRequested { get; set; }

        [StringLength(100)]
        public string? DeliveryRecipientName { get; set; }

        [StringLength(150)]
        public string? DeliveryAddressLine1 { get; set; }

        [StringLength(150)]
        public string? DeliveryAddressLine2 { get; set; }

        [StringLength(80)]
        public string? DeliveryCity { get; set; }

        [StringLength(80)]
        public string? DeliveryState { get; set; }

        [StringLength(10)]
        public string? DeliveryPostcode { get; set; }

        [StringLength(30)]
        public string? DeliveryRegion { get; set; }

        /*
         * Fee snapshot
         *
         * These values are saved when the application is submitted.
         * Later changes to fees or discount rates will not modify
         * the original application amount.
         */
        [Range(typeof(decimal), "0", "9999999")]
        public decimal MonthlyTuitionFeeAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "9999999")]
        public decimal TuitionSubtotalAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "100")]
        public decimal DiscountRateAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "9999999")]
        public decimal DiscountAmountAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "9999999")]
        public decimal DiscountedTuitionAtApplication
        {
            get;
            set;
        }

        [Range(0, int.MaxValue)]
        public int HardcopySubjectCountAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "9999999")]
        public decimal HardcopyFeeAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "9999999")]
        public decimal ShippingFeeAtApplication
        {
            get;
            set;
        }

        [Range(typeof(decimal), "0", "9999999")]
        public decimal EstimatedTotalAtApplication
        {
            get;
            set;
        }

        /*
         * Review information
         */
        public EnrollmentStatus Status { get; set; }
            = EnrollmentStatus.PendingReview;

        [StringLength(500)]
        public string? ApplicantNotes { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime SubmittedAt { get; set; }
            = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedByUserId { get; set; }

        /*
         * Navigation properties
         */
        public ApplicationUser? SubmittedByUser { get; set; }

        public ApplicationUser? ReviewedByUser { get; set; }

        public ParentProfile? ExistingParentProfile { get; set; }

        public StudentProfile? ExistingStudentProfile { get; set; }

        public ICollection<EnrollmentApplicationSubject>
            SelectedSubjects
        { get; set; }
            = new List<EnrollmentApplicationSubject>();

        public ICollection<StudentEnrollment>
            StudentEnrollments
        { get; set; }
            = new List<StudentEnrollment>();

        public ICollection<EnrollmentStatusHistory>
            StatusHistories
        { get; set; }
            = new List<EnrollmentStatusHistory>();
    }
}
