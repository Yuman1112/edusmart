using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Identity;
public enum EmailChangeStatus { VerifyCurrent = 0, VerifyNew = 1, Completed = 2, Cancelled = 3 }
public sealed class EmailChangeRequest
{
    public Guid Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(254)] public string NewEmail { get; set; } = "";
    [MaxLength(254)] public string? OriginalEmail { get; set; }
    [Required, MaxLength(64)] public string SecurityStampHash { get; set; } = "";
    [Required, MaxLength(64)] public string RecipientHash { get; set; } = "";
    [Required, MaxLength(512)] public string CodeHash { get; set; } = "";
    public Guid CodeNonce { get; set; }
    public Guid LinkNonce { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CodeExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public EmailChangeStatus Status { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
