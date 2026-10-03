using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Communication
{
    public class AnnouncementRead
    {
        public int AnnouncementReadId { get; set; }

        public int AnnouncementId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime ReadAt { get; set; }
            = DateTime.UtcNow;

        public Announcement Announcement { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;
    }
}
