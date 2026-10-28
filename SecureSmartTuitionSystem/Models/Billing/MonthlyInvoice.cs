using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Models.Billing;
public enum MonthlyInvoiceStatus { Pending, Paid, Cancelled }
public sealed class MonthlyInvoice
{
    public int MonthlyInvoiceId { get; set; }
    public int StudentProfileId { get; set; }
    public DateTime Month { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal Total { get; set; }
    public MonthlyInvoiceStatus Status { get; set; }
    public Guid CheckoutKey { get; set; } = Guid.NewGuid();
    [MaxLength(255)] public string? CheckoutSessionId { get; set; }
    [MaxLength(255)] public string? PaymentIntentId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;
    public ICollection<MonthlyInvoiceLine> Lines { get; set; } = new List<MonthlyInvoiceLine>();
    [NotMapped] public string Number => $"MB{MonthlyInvoiceId:D7}";
}
public sealed class MonthlyInvoiceLine
{
    public int MonthlyInvoiceLineId { get; set; }
    public int MonthlyInvoiceId { get; set; }
    public int StudentEnrollmentId { get; set; }
    [MaxLength(250)] public string Description { get; set; } = "";
    [Column(TypeName="decimal(10,2)")] public decimal Amount { get; set; }
    public MonthlyInvoice MonthlyInvoice { get; set; } = null!;
}
// Durable daily notification/email record. Unique invoice/user/type/date prevents
// duplicate in-app alerts even when a worker retries or two instances run.
public sealed class BillingReminder
{
    public long BillingReminderId { get; set; }
    public int MonthlyInvoiceId { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(30)] public string Kind { get; set; } = "";
    public DateTime Day { get; set; }
    public DateTime? EmailAttemptedAtUtc { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    [MaxLength(300)] public string? EmailError { get; set; }
    public MonthlyInvoice MonthlyInvoice { get; set; } = null!;
}
