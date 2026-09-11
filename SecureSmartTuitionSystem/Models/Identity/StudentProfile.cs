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
        [StringLength(30)]
        public string IdentificationType { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string IdentificationNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(10)]
        public string Gender { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string School { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string AcademicLevel { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // OLD relationship - temporarily kept during migration.
        public int ParentProfileId { get; set; }

        public ParentProfile ParentProfile { get; set; } = null!;

        // NEW Parent-Student relationship.
        public ICollection<ParentStudent> ParentStudents { get; set; }
            = new List<ParentStudent>();

        public string? ApplicationUserId { get; set; }

        public ApplicationUser? ApplicationUser { get; set; }
    }
}