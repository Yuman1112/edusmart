using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    /*
     * =========================================================
     * ADMIN ENROLLMENT LIST PAGE
     * =========================================================
     */

    public class AdminEnrollmentIndexViewModel
    {
        public List<AdminEnrollmentListItemViewModel> Applications
        {
            get;
            set;
        } = new();

        /*
         * Filter values
         */

        public EnrollmentStatus? Status { get; set; }
        public string SortBy { get; set; } = "submittedAt";

        public string SortDirection { get; set; } = "desc";

        /*
         * Summary values
         */

        public int TotalApplicationCount { get; set; }

        public int PendingReviewCount { get; set; }

        public int ApprovedPendingPaymentCount { get; set; }

        public int EnrolledCount { get; set; }
    }

    public class AdminEnrollmentListItemViewModel
    {
        public int EnrollmentApplicationId { get; set; }

        public string ApplicationNumber { get; set; }
            = string.Empty;

        public string StudentName { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string GuardianName { get; set; }
            = string.Empty;

        public string GuardianEmail { get; set; }
            = string.Empty;

        public int SelectedClassCount { get; set; }

        public decimal TotalMonthlyFee { get; set; }

        public EnrollmentStatus Status { get; set; }

        public DateTime SubmittedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }

    /*
     * =========================================================
     * ADMIN ENROLLMENT DETAILS PAGE
     * =========================================================
     */

    public class AdminEnrollmentDetailsViewModel
    {
        public List<StudentDuplicateCandidate> DuplicateCandidates { get; set; } = new();
        public string DuplicateReviewVersion { get; set; } = string.Empty;

        public int EnrollmentApplicationId { get; set; }

        public string ApplicationNumber { get; set; }
            = string.Empty;

        /*
         * Student information
         */

        public string StudentName { get; set; }
            = string.Empty;

        public DateTime StudentDateOfBirth { get; set; }

        public string AcademicLevel { get; set; }
            = string.Empty;

        public int? ExistingStudentProfileId { get; set; }

        /*
         * Guardian information
         */

        public string GuardianName { get; set; }
            = string.Empty;

        public string GuardianEmail { get; set; }
            = string.Empty;

        public string GuardianPhoneNumber { get; set; }
            = string.Empty;

        public string? GuardianIdentificationLast4 { get; set; }

        public int? ExistingParentProfileId { get; set; }

        /*
         * Application information
         */

        public DateTime RequestedStartDate { get; set; }

        public bool HardcopyMaterialsRequested { get; set; }

        public bool FinalTwoWeeksConfirmed { get; set; }

        public string? ApplicantNotes { get; set; }

        public string? RejectionReason { get; set; }

        public EnrollmentStatus Status { get; set; }

        public DateTime SubmittedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedByName { get; set; }

        /*
         * Fee and class information
         */

        public decimal TotalMonthlyFee { get; set; }

        public List<AdminEnrollmentClassItemViewModel>
            SelectedClasses
        {
            get;
            set;
        } = new();

        /*
         * Status history
         */

        public List<AdminEnrollmentHistoryItemViewModel>
            StatusHistories
        {
            get;
            set;
        } = new();

        /*
         * Control which buttons can appear.
         */

        public bool CanApprove =>
            Status == EnrollmentStatus.PendingReview;

        public bool CanReject =>
            Status == EnrollmentStatus.PendingReview;

        public bool CanCancel =>
            Status == EnrollmentStatus.ApprovedPendingPayment;
    }

    /*
     * =========================================================
     * SELECTED CLASS INFORMATION
     * =========================================================
     */

    public class AdminEnrollmentClassItemViewModel
    {
        public int EnrollmentApplicationSubjectId { get; set; }

        public int ClassSessionId { get; set; }

        public string ClassName { get; set; }
            = string.Empty;

        public string SubjectName { get; set; }
            = string.Empty;

        public string EducationStage { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string TutorName { get; set; }
            = string.Empty;

        public string Day { get; set; }
            = string.Empty;

        public string StartTime { get; set; }
            = string.Empty;

        public string EndTime { get; set; }
            = string.Empty;

        public decimal MonthlyFeeAtApplication { get; set; }

        public int Capacity { get; set; }

        public int EnrolledStudentCount { get; set; }

        public ClassSessionStatus ClassStatus { get; set; }

        public bool IsFull =>
            EnrolledStudentCount >= Capacity;

        public int AvailableSeatCount =>
            Math.Max(0, Capacity - EnrolledStudentCount);

        public bool IsAvailable =>
            ClassStatus == ClassSessionStatus.Active &&
            !IsFull;
    }

    /*
     * =========================================================
     * ENROLLMENT STATUS HISTORY
     * =========================================================
     */

    public class AdminEnrollmentHistoryItemViewModel
    {
        public int EnrollmentStatusHistoryId { get; set; }

        public EnrollmentStatus? PreviousStatus { get; set; }

        public EnrollmentStatus NewStatus { get; set; }

        public DateTime ChangedAt { get; set; }

        public string ChangedByName { get; set; }
            = "System";

        public string? Remarks { get; set; }
    }

    /*
     * =========================================================
     * APPROVE FORM
     * =========================================================
     */

    public class ApproveEnrollmentViewModel
    {
        public bool DuplicateReviewConfirmed { get; set; }

        [StringLength(64)]
        public string? DuplicateReviewVersion { get; set; }

        [StringLength(200)]
        public string? DuplicateReviewReason { get; set; }

        [Required]
        public int EnrollmentApplicationId { get; set; }

        [StringLength(
            500,
            ErrorMessage =
                "Remarks cannot exceed 500 characters.")]
        [Display(Name = "Approval Remarks")]
        public string? Remarks { get; set; }
    }

    /*
     * =========================================================
     * REJECT FORM
     * =========================================================
     */

    public class RejectEnrollmentViewModel
    {
        [Required]
        public int EnrollmentApplicationId { get; set; }

        [Required(
            ErrorMessage =
                "Please provide a rejection reason.")]
        [StringLength(
            500,
            MinimumLength = 5,
            ErrorMessage =
                "Rejection reason must contain between " +
                "5 and 500 characters.")]
        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; }
            = string.Empty;
    }

    /*
     * =========================================================
     * CANCEL FORM
     * =========================================================
     */

    public class CancelEnrollmentViewModel
    {
        [Required]
        public int EnrollmentApplicationId { get; set; }

        [Required(
            ErrorMessage =
                "Please provide a cancellation reason.")]
        [StringLength(
            500,
            MinimumLength = 5,
            ErrorMessage =
                "Cancellation reason must contain between " +
                "5 and 500 characters.")]
        [Display(Name = "Cancellation Reason")]
        public string CancellationReason { get; set; }
            = string.Empty;
    }
}
