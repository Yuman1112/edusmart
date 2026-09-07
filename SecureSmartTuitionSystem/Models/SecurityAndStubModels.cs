using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models;

/// <summary>System Security Module: append-only audit trail of every state-changing action.</summary>
public class ActivityLog
{
    public long ActivityLogId { get; set; }

    /// <summary>Null for unauthenticated/system-triggered actions (e.g. the billing scheduler).</summary>
    public string? UserId { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = null!; // e.g. "Enrollment.Approve", "Payment.Confirm"

    [MaxLength(60)]
    public string? EntityName { get; set; }

    [MaxLength(60)]
    public string? EntityId { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(1000)]
    public string? Details { get; set; }

    public bool IsSuccess { get; set; } = true;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>System Security Module: structured error record; only the TraceId is ever shown to the user.</summary>
public class ErrorLog
{
    public long ErrorLogId { get; set; }

    [Required, MaxLength(60)]
    public string TraceId { get; set; } = null!;

    [MaxLength(300)]
    public string? Path { get; set; }

    [MaxLength(200)]
    public string? ExceptionType { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }

    public string? StackTrace { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>[STUB - Learning &amp; Assessment Module] Needed only for deadline notifications + the submission-rate report.</summary>
public class Assignment
{
    public int AssignmentId { get; set; }

    [Required]
    public int ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;

    public DateTime DueDate { get; set; }

    public bool LateSubmissionAllowed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}

/// <summary>[STUB - Learning &amp; Assessment Module]</summary>
public class AssignmentSubmission
{
    public int SubmissionId { get; set; }

    [Required]
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    [Required]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public DateTime? SubmittedAt { get; set; }
    public bool IsLate { get; set; }
    public decimal? Score { get; set; }
    public DateTime? GradedAt { get; set; }
}
