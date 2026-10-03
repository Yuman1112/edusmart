using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Communication
{
    public class ChatMessage
    {
        public int ChatMessageId { get; set; }

        public int ConversationId { get; set; }

        [Required]
        public string SenderUserId { get; set; } = string.Empty;

        public string? ProtectedContent { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        public DateTime? EditedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        public string? DeletedByUserId { get; set; }

        public bool IsDeletedForEveryone { get; set; }

        public Conversation Conversation { get; set; } = null!;

        public ApplicationUser SenderUser { get; set; } = null!;

        public ICollection<MessageAttachment> Attachments { get; set; }
            = new List<MessageAttachment>();

        public ICollection<MessageUserDeletion> UserDeletions { get; set; }
            = new List<MessageUserDeletion>();
    }
}
