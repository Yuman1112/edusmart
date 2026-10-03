using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Communication
{
    public class MessageAttachment
    {
        public int MessageAttachmentId { get; set; }

        public int ChatMessageId { get; set; }

        [Required, StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, StringLength(80)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required, StringLength(64)]
        public string Sha256Hash { get; set; } = string.Empty;

        public ChatMessage ChatMessage { get; set; } = null!;
    }
}
