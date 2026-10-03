using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Communication
{
    public sealed class MessageUserDeletion
    {
        public int MessageUserDeletionId { get; set; }

        public int ChatMessageId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;

        public ChatMessage ChatMessage { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;
    }
}
