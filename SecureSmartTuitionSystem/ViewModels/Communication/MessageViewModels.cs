using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Communication
{
    public sealed class MessageIndexViewModel
    {
        public int TotalUnreadCount => Conversations.Sum(item => item.UnreadCount);
        public bool IsAdmin { get; set; }
        public int? ActiveConversationId { get; set; }
        public List<ConversationListItemViewModel> Conversations { get; set; } = new();
        public List<ParentContactViewModel> AvailableParents { get; set; } = new();
        public List<ChatMessageItemViewModel> Messages { get; set; } = new();
        public string ActiveParticipantName { get; set; } = string.Empty;
        public string ActiveParticipantEmail { get; set; } = string.Empty;
    }

    public sealed class ParentContactViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ParentUserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public sealed class ConversationListItemViewModel
    {
        public int ConversationId { get; set; }
        public string ParticipantUserId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string ParticipantEmail { get; set; } = string.Empty;
        public string LastMessagePreview { get; set; } = "No messages yet";
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }

    public sealed class ChatMessageItemViewModel
    {
        public int ChatMessageId { get; set; }
        public bool IsMine { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeletedForEveryone { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDeleteForEveryone { get; set; }
        public List<MessageAttachmentViewModel> Attachments { get; set; } = new();
    }

    public sealed class MessageAttachmentViewModel
    {
        public int MessageAttachmentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public sealed class SendMessageViewModel
    {
        [Range(1, int.MaxValue)]
        public int ConversationId { get; set; }

        [StringLength(2000)]
        public string? Content { get; set; }

        public IFormFile? Attachment { get; set; }
    }

    public sealed class EditMessageViewModel
    {
        [Range(1, int.MaxValue)]
        public int ChatMessageId { get; set; }

        [Required(ErrorMessage = "Message content is required.")]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;
    }
}
