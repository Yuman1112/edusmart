using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Models.Communication;
public enum EnrollmentNoticeState { Pending, Sending, Sent, Failed, Unconfirmed }
[Index(nameof(EnrollmentApplicationId), nameof(Kind), nameof(RecipientRole), IsUnique = true)]
[Index(nameof(State), nameof(CreatedAtUtc))]
public sealed class EnrollmentNotice
{
    public long EnrollmentNoticeId { get; set; }
    public int EnrollmentApplicationId { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "";
    [MaxLength(20)] public string RecipientRole { get; set; } = "";
    public string ProtectedPayload { get; set; } = "";
    public EnrollmentNoticeState State { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public Guid? AttemptId { get; set; }
    [MaxLength(200)] public string? Note { get; set; }
    public EnrollmentApplication EnrollmentApplication { get; set; } = null!;
}
