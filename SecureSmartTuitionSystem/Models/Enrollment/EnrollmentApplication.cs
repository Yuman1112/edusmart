using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public class EnrollmentApplication
    {
        public int EnrollmentApplicationId { get; set; }

        [Required]
        [StringLength(30)]
        public string ApplicationNumber { get; set; } = string.Empty;

        // Nullable because a prospective applicant can apply
        // without logging in.
        public string? SubmittedByUserId { get; set; }

        public int? ExistingParentProfileId { get; set; }

        public int? ExistingStudentProfileId { get; set; }

        [Required]
        [StringLength(100)]
        public string GuardianName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string GuardianEmail { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(30)]
        public string GuardianPhoneNumber { get; set; } = string.Empty;

        [StringLength(4, MinimumLength = 4)]
        public string? GuardianIdentificationLast4 { get; set; }

        [Required]
        [StringLength(100)]
        public string StudentName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime StudentDateOfBirth { get; set; }

        [Required]
        [StringLength(30)]
        public string AcademicLevel { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime RequestedStartDate { get; set; }

        public bool HardcopyMaterialsRequested { get; set; }

        public bool FinalTwoWeeksConfirmed { get; set; }

        public bool PotentialDuplicateStudent { get; set; }

        public bool PotentialDuplicateGuardian { get; set; }

        public EnrollmentStatus Status { get; set; }
            = EnrollmentStatus.PendingReview;

        [StringLength(500)]
        public string? ApplicantNotes { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedByUserId { get; set; }

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