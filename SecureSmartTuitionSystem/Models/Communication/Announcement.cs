using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Communication
{
    public class Announcement
    {
        public int AnnouncementId { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = string.Empty;

        public AnnouncementAudience TargetAudience { get; set; }
            = AnnouncementAudience.All;

        public DateTime PublishAt { get; set; }
            = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public bool IsArchived { get; set; }

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        public ApplicationUser CreatedByUser { get; set; } = null!;

        public ICollection<AnnouncementRead> Reads { get; set; }
            = new List<AnnouncementRead>();
    }
}
