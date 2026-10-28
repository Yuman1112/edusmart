using SecureSmartTuitionSystem.Models.Identity;
using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models
{
    public class TutorProfile
    {
        public int TutorProfileId { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Qualification { get; set; } = string.Empty;

        [StringLength(500)]
        public string? TeachingExperience { get; set; }

        // Optional: Additional teaching expertise.
        [StringLength(200)]
        public string? AreaOfExpertise { get; set; }

        [EnumDataType(typeof(SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType))]
        public SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType EmploymentType { get; set; }
            = SecureSmartTuitionSystem.Models.Tutor.TutorEmploymentType.PartTime;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ApplicationUser ApplicationUser { get; set; } = null!;

        // One Tutor can teach multiple levels and subjects.
        public ICollection<TutorTeachingSubject> TeachingSubjects
        { get; set; } = new List<TutorTeachingSubject>();
    }
}
