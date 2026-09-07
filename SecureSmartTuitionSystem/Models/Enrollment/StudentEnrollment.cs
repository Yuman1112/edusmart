using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public enum StudentEnrollmentStatus
    {
        Active,
        PendingDrop,
        Dropped,
        Completed
    }

    public class StudentEnrollment
    {
        public int StudentEnrollmentId { get; set; }

        public int StudentProfileId { get; set; }

        public int ClassSessionId { get; set; }

        public int? EnrollmentApplicationId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

        public DateTime? DropRequestedAt { get; set; }

        // Dropping a subject only takes effect
        // in the following billing month.
        public DateTime? DropEffectiveDate { get; set; }

        public DateTime? EndedAt { get; set; }

        public bool HardcopyMaterialsRequested { get; set; }

        public decimal MonthlyFeeAtEnrollment { get; set; }

        public StudentEnrollmentStatus Status { get; set; }
            = StudentEnrollmentStatus.Active;

        public StudentProfile StudentProfile { get; set; } = null!;

        public ClassSession ClassSession { get; set; } = null!;

        public EnrollmentApplication? EnrollmentApplication { get; set; }
    }
}