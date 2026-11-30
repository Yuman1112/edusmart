using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace SecureSmartTuitionSystem.Models.Communication;
public enum GuestReplyState { None, Sending, Sent, Failed, Unconfirmed }
[Index(nameof(SubmissionKey), IsUnique = true)]
[Index(nameof(IsResolved), nameof(CreatedAtUtc))]
public sealed class GuestEnquiry
{
    public int GuestEnquiryId { get; set; }
    public Guid SubmissionKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
    public GuestReplyState ReplyState { get; set; }
    public DateTime? ReplyClaimedAtUtc { get; set; }
    public string? ProtectedConversation { get; set; }
    public string? ProtectedRecoveryHistory { get; set; }
    public Guid? ReplyAttemptId { get; set; }
    public DateTime? ReplySentAtUtc { get; set; }
    public string? ProtectedReply { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    // Name, email and message are one encrypted payload; no guest login is created.
    [Required, MaxLength(24000)] public string ProtectedContact { get; set; } = "";
}
