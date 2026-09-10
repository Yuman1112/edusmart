using SecureSmartTuitionSystem.Models.Identity;
using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models
{
    public class AdminProfile
    {
        public int AdminProfileId { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Position { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser ApplicationUser { get; set; } = null!;
    }
}