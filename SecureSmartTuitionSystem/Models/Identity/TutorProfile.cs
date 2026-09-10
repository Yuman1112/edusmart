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

        [Required]
        [StringLength(200)]
        public string AreaOfExpertise { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ApplicationUser ApplicationUser { get; set; } = null!;
    }
}