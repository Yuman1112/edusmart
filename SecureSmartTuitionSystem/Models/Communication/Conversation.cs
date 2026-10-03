using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Communication
{
    public class Conversation
    {
        public int ConversationId { get; set; }

        [Required]
        public string ParentUserId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastMessageAt { get; set; }

        public bool IsClosed { get; set; }

        public ApplicationUser ParentUser { get; set; } = null!;

        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}
