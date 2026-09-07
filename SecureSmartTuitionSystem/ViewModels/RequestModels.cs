using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels;

/*
 * All input DTOs use Data Annotations so ASP.NET Core MVC's built-in model
 * validation runs automatically (both client-side, via unobtrusive
 * validation on the generated form, and — authoritatively — server-side on
 * every POST). This is the System Security Module's "Input Validation"
 * technique (Ch.2.5 / Table 4.27): "Server-side validation is applied
 * regardless of whether client-side validation has already been performed."
 */

/// <summary>Table 4.4 "Submit Enrollment Request".</summary>
public class EnrollmentSubmitRequest
{
    /// <summary>The primary class session the applicant enrolls into (Table 4.4 step 1 "Actor selects a class").</summary>
    [Required]
    public int ClassSessionId { get; set; }

    /// <summary>
    /// Every subject the family is enrolling for, selected via the
    /// multiple-selection checkbox interface (Functional Requirements:
    /// "allow multiple subjects to be selected during enrollment"). Must
    /// include ClassSessionId's own subject if that subject is also being
    /// billed; subjects here are billed even when no separate ClassSession
    /// exists yet for them.
    /// </summary>
    [Required, MinLength(1, ErrorMessage = "Select at least one subject.")]
    public List<int> SubjectIds { get; set; } = new();

    [Required, StringLength(150, MinimumLength = 2)]
    public string StudentFullName { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    public DateOnly StudentBirthDate { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string GuardianFullName { get; set; } = null!;

    [Required, EmailAddress, StringLength(256)]
    public string GuardianEmail { get; set; } = null!;

    [Required, StringLength(30, MinimumLength = 4)]
    public string GuardianPhoneNumber { get; set; } = null!;

    [Required, StringLength(30, MinimumLength = 4)]
    public string GuardianIdentificationNo { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    public DateOnly RequestedStartDate { get; set; }

    public bool HardcopyMaterialsOptIn { get; set; }

    /// <summary>Must be true when the server has determined the start date requires double confirmation (Functional Requirements: Class and Enrollment).</summary>
    public bool DoubleConfirmedLateMonthStart { get; set; }

    /// <summary>True if this applicant already has an authenticated account (existing guardian adding another child).</summary>
    public bool IsExistingUser { get; set; }
}

/// <summary>Table 4.5 "Approve / Reject Enrollment".</summary>
public class EnrollmentDecisionRequest
{
    [Required]
    public int EnrollmentId { get; set; }

    [Required]
    public bool Approve { get; set; }

    [StringLength(300)]
    public string? RejectionReason { get; set; }
}

/// <summary>Table 4.12 "Add/Drop Subject Billing Adjustment".</summary>
public class SubjectChangeRequestModel
{
    [Required]
    public int StudentId { get; set; }

    [Required]
    public int SubjectId { get; set; }

    [Required]
    public bool IsAdd { get; set; } // true = Add, false = Drop

    [Required]
    public bool DoubleConfirmed { get; set; }
}

/// <summary>Table 4.9 "Make Payment".</summary>
public class MakePaymentRequest
{
    /// <summary>Either the invoice id (authenticated flow) or the secure one-time token (initial-enrollment flow) must be supplied.</summary>
    public int? InvoiceId { get; set; }
    public string? PaymentLinkToken { get; set; }

    [Required, StringLength(30)]
    public string PaymentMethod { get; set; } = null!;

    /// <summary>Raw entered card/bank details — forwarded straight to the gateway, never persisted (Ch.2.3 PCI-DSS alignment).</summary>
    [Required]
    public string RawPaymentDetails { get; set; } = null!;

    [StringLength(300)]
    public string? PayerNote { get; set; }
}

/// <summary>Table 4.15 "Send and Receive Messages".</summary>
public class SendMessageRequest
{
    public int? ConversationId { get; set; }

    /// <summary>Required only when starting a brand-new conversation.</summary>
    public string? RecipientUserId { get; set; }

    [Required(ErrorMessage = "Message cannot be empty.")]
    [StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = null!;
}

/// <summary>Table 4.14 "AI Chatbot FAQ".</summary>
public class ChatbotAskRequest
{
    public int? SelectedFaqId { get; set; }

    [StringLength(500)]
    public string? FreeTextQuestion { get; set; }
}

/// <summary>Functional Requirements: "broadcast announcements to parents and track their delivery and read status".</summary>
public class BroadcastAnnouncementRequest
{
    [Required, StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = null!;

    [Required, StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = null!;

    /// <summary>Empty = broadcast to every Guardian account.</summary>
    public List<string> RecipientUserIds { get; set; } = new();
}

/// <summary>Table 4.21 "Data Filtering by Subject/Class" — shared filter shape for every Reporting &amp; Analytics endpoint.</summary>
public class ReportFilterRequest
{
    public int? SubjectId { get; set; }
    public int? ClassSessionId { get; set; }
    public string? FormYear { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    [RegularExpression("^(pdf|excel)$", ErrorMessage = "Export format must be 'pdf' or 'excel'.")]
    public string? ExportFormat { get; set; }
}
