using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public class EnrollmentStatusHistory
    {
        public int EnrollmentStatusHistoryId { get; set; }

        public int EnrollmentApplicationId { get; set; }

        public EnrollmentStatus? PreviousStatus { get; set; }

        public EnrollmentStatus NewStatus { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public string? ChangedByUserId { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        public EnrollmentApplication EnrollmentApplication { get; set; }
            = null!;

        public ApplicationUser? ChangedByUser { get; set; }
    }
}