using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Models.Communication;
public sealed class UserNotification
{
    public long UserNotificationId { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    [Required, MaxLength(150)] public string Title { get; set; } = "";
    [Required, MaxLength(1000)] public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AvailableFromDate { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
