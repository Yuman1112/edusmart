using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public enum ClassSessionStatus
    {
        Active,
        Cancelled,
        Completed
    }

    public class ClassSession
    {
        public int ClassSessionId { get; set; }

        [Required]
        [StringLength(30)]
        public string ClassCode { get; set; } = string.Empty;

        public int SubjectId { get; set; }

        [Required]
        public string TutorUserId { get; set; } = string.Empty;

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        [Required]
        [StringLength(100)]
        public string Venue { get; set; } = string.Empty;

        [Range(1, 100)]
        public int Capacity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ClassSessionStatus Status { get; set; }
            = ClassSessionStatus.Active;

        public Subject Subject { get; set; } = null!;

        public ApplicationUser TutorUser { get; set; } = null!;
    }
}