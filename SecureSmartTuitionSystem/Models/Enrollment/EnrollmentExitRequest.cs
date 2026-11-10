using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SecureSmartTuitionSystem.Models.Billing;
namespace SecureSmartTuitionSystem.Models.Enrollment;
public enum ExitRefundStatus { NoRefundDue, PendingReview, Processing, PendingProvider, Completed, Rejected, Failed, Cancelled }
public sealed class EnrollmentExitRequest
{
    public int EnrollmentExitRequestId { get; set; }
    public int StudentEnrollmentId { get; set; }
    public int? PaymentId { get; set; }
    [Required, MaxLength(450)] public string RequestedByUserId { get; set; } = "";
    [Required, MaxLength(500)] public string Reason { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    [DataType(DataType.Date)] public DateTime EffectiveDate { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal EstimatedRefund { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal ApprovedRefund { get; set; }
    public ExitRefundStatus RefundStatus { get; set; }
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(500)] public string? ReviewNote { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    [MaxLength(450)] public string? CancelledByUserId { get; set; }
    public DateTime? RefundStartedAtUtc { get; set; }
    public DateTime? RefundCompletedAtUtc { get; set; }
    [MaxLength(255)] public string? ProviderRefundId { get; set; }
    [MaxLength(500)] public string? LastError { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public StudentEnrollment StudentEnrollment { get; set; } = null!;
    public Payment? Payment { get; set; }
}
