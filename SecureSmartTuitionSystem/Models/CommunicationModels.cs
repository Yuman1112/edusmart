using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureSmartTuitionSystem.Models;

public class Notification
{
    public int NotificationId { get; set; }

    [Required]
    public string UserId { get; set; } = null!;
    public ApplicationUser? User { get; set; }

    public NotificationType NotificationType { get; set; }

    /// <summary>Id of the related Invoice/Assignment/Enrollment/etc., when applicable.</summary>
    public int? ReferenceId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Conversation
{
    public int ConversationId { get; set; }

    [MaxLength(150)]
    public string? Subject { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class ConversationParticipant
{
    public int ConversationParticipantId { get; set; }

    [Required]
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = null!;
    public ApplicationUser? User { get; set; }

    /// <summary>Soft-delete: "Delete Conversation" (Table 4.16) removes it from this user's view only.</summary>
    public bool IsDeletedForUser { get; set; }
}

public class Message
{
    public int MessageId { get; set; }

    [Required]
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    [Required]
    public string SenderUserId { get; set; } = null!;
    public ApplicationUser? Sender { get; set; }

    /// <summary>Stored as plain text; Razor Views HTML-encode automatically on render (Ch.2.5 Output Encoding), so no raw HTML ever executes.</summary>
    [Required, MaxLength(2000)]
    public string Body { get; set; } = null!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsDeliveredRealtime { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}

public class MessageAttachment
{
    public int MessageAttachmentId { get; set; }

    [Required]
    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;

    [Required, MaxLength(260)]
    public string FileName { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = null!;

    public int FileSizeBytes { get; set; }

    [Required, MaxLength(400)]
    public string StoragePath { get; set; } = null!;
}

/// <summary>Rule-based, menu-driven FAQ entry (Ch.2.3 Adopted Approach: Communication &amp; Announcement Module).</summary>
public class ChatbotFaq
{
    public int ChatbotFaqId { get; set; }

    [Required, MaxLength(60)]
    public string MenuCategory { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Question { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string Answer { get; set; } = null!;

    [MaxLength(10)]
    public string Language { get; set; } = "EN";

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ChatbotKeyword> Keywords { get; set; } = new List<ChatbotKeyword>();
}

/// <summary>Free-text keyword layer supplementing the menu (Lokman &amp; Zain one-match/all-match categories, Ch.2.3).</summary>
public class ChatbotKeyword
{
    public int ChatbotKeywordId { get; set; }

    [Required]
    public int ChatbotFaqId { get; set; }
    public ChatbotFaq ChatbotFaq { get; set; } = null!;

    [Required, MaxLength(60)]
    public string Keyword { get; set; } = null!;

    /// <summary>"Any" = one-match (this keyword alone is enough); "All" = every keyword tagged All for this FAQ must appear.</summary>
    [MaxLength(10)]
    public string MatchMode { get; set; } = "Any";
}

public class ChatbotLog
{
    public int ChatbotLogId { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    [Required, MaxLength(500)]
    public string QuestionText { get; set; } = null!;

    public int? MatchedChatbotFaqId { get; set; }
    public ChatbotFaq? MatchedChatbotFaq { get; set; }

    public bool WasEscalated { get; set; }

    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
}

public class Announcement
{
    public int AnnouncementId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Body { get; set; } = null!;

    [Required]
    public string PostedByUserId { get; set; } = null!;

    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AnnouncementRecipient> Recipients { get; set; } = new List<AnnouncementRecipient>();
}

/// <summary>Per-recipient delivery/read tracking (Functional Requirements: "track their delivery and read status").</summary>
public class AnnouncementRecipient
{
    public int AnnouncementRecipientId { get; set; }

    [Required]
    public int AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = null!;

    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
