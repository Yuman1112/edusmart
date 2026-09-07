using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureSmartTuitionSystem.Models;

public class Invoice
{
    public int InvoiceId { get; set; }

    [Required]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int? EnrollmentId { get; set; }
    public Enrollment? Enrollment { get; set; }

    /// <summary>First day of the billed month.</summary>
    public DateOnly BillingMonth { get; set; }

    public InvoiceType InvoiceType { get; set; } = InvoiceType.Monthly;

    [Column(TypeName = "decimal(10,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal HardcopyFee { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal AdditionalCharge { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.PendingPayment;

    public DateOnly DueDate { get; set; }

    /// <summary>AES-256 encrypted at the application layer before EF Core persists it (Ch.2.3 Adopted Approach).</summary>
    public string? RemarksEncrypted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>How many days overdue this invoice is, or 0/negative if not yet due. Drives the 4-7 / &gt;7 day grace-period rules.</summary>
    [NotMapped]
    public int DaysOverdue => Status == InvoiceStatus.Paid || Status == InvoiceStatus.Cancelled
        ? 0
        : Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - DueDate.DayNumber);
}

public class InvoiceLineItem
{
    public int InvoiceLineItemId { get; set; }

    [Required]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = null!;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }
}

/// <summary>
/// A payment attempt/transaction. Per PCI-DSS alignment (Ch.2.3), the raw
/// card/bank number is never captured or stored here — only the non-sensitive
/// reference token returned by the third-party payment gateway, which is
/// itself AES-256 encrypted at rest via the AppDbContext value converter.
/// </summary>
public class Payment
{
    public int PaymentId { get; set; }

    [Required]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; }

    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = null!; // Card | OnlineBanking | EWallet

    /// <summary>AES-256 encrypted tokenised gateway reference (never a raw card/bank number).</summary>
    public string GatewayTokenEncrypted { get; set; } = null!;

    /// <summary>AES-256 encrypted optional payer note.</summary>
    public string? PayerNoteEncrypted { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(300)]
    public string? FailureReason { get; set; }

    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Receipt? Receipt { get; set; }
}

public class Receipt
{
    public int ReceiptId { get; set; }

    [Required]
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    [Required, MaxLength(30)]
    public string ReceiptNumber { get; set; } = null!;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Secure, time-limited, single-use link e-mailed after enrollment approval (Functional Requirements: Class and Enrollment).</summary>
public class PaymentLink
{
    public int PaymentLinkId { get; set; }

    [Required]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    [NotMapped]
    public bool IsValid => UsedAt is null && DateTime.UtcNow <= ExpiresAt;
}
