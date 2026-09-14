using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Identity
{
    public class ParentProfile
    {
        public int ParentProfileId { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        // Full IC number
        [StringLength(14)]
        public string? IdentificationNumber { get; set; }

        // Used for existing-parent verification
        [StringLength(4, MinimumLength = 4)]
        public string? IdentificationLast4 { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser ApplicationUser { get; set; } = null!;

        public ICollection<ParentStudent> ParentStudents { get; set; }
            = new List<ParentStudent>();
    }
}