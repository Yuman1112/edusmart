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
        [Display(Name = "Class Code")]
        public string ClassCode { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Class Name")]
        public string? ClassName { get; set; }

        [StringLength(
            500,
            ErrorMessage = "Remark cannot exceed 500 characters.")]
        [Display(Name = "Remark")]
        public string? Remark { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [Required]
        public string TutorUserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Class Day")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeOnly StartTime { get; set; }

        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "End Time")]
        public TimeOnly EndTime { get; set; }

        [Required]
        [Url]
        [StringLength(250)]
        [Display(Name = "Google Meet Link")]
        public string Venue { get; set; } = string.Empty;

        [Required]
        [Range(1, 100)]
        public int Capacity { get; set; }

        [DataType(DataType.Date)] public DateTime? StartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public ClassSessionStatus Status { get; set; }
            = ClassSessionStatus.Active;

        public Subject Subject { get; set; } = null!;

        public ApplicationUser TutorUser { get; set; } = null!;
    }
}
