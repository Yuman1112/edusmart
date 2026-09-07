using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Identity
{
    public class StudentProfile
    {
        public int StudentProfileId { get; set; }

        [Required]
        [StringLength(20)]
        public string StudentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(30)]
        public string AcademicLevel { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int ParentProfileId { get; set; }

        public ParentProfile ParentProfile { get; set; } = null!;

        public string? ApplicationUserId { get; set; }

        public ApplicationUser? ApplicationUser { get; set; }
    }
}