using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Models.Communication;
// Personal list preferences do not grant access or delete another participant's history.
public sealed class ConversationUserPreference
{
    public long ConversationUserPreferenceId { get; set; }
    public int ConversationId { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public bool IsPinned { get; set; }
    public bool IsHidden { get; set; }
    public DateTime? HiddenAtUtc { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
