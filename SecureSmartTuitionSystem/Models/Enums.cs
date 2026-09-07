namespace SecureSmartTuitionSystem.Models;

public enum EnrollmentStatus
{
    Pending,
    FlaggedDuplicate,
    Approved,
    Rejected
}

public enum ClassSessionStatus
{
    Active,
    Cancelled,
    Rescheduled
}

public enum InvoiceStatus
{
    PendingPayment,
    Paid,
    Overdue,
    Cancelled
}

public enum InvoiceType
{
    Enrollment,
    Monthly,
    AddDropAdjustment
}

public enum PaymentStatus
{
    Pending,
    Success,
    Failed
}

public enum SubjectChangeType
{
    Add,
    Drop
}

public enum SubjectChangeStatus
{
    Pending,
    Applied,
    Rejected
}

/// <summary>
/// Every distinct alert the Communication &amp; Announcement Module can raise.
/// Kept as an enum (rather than free text) so the "once per type per day"
/// throttling rule (Functional Requirements, Communication and Announcement)
/// can be enforced reliably.
/// </summary>
public enum NotificationType
{
    BillGenerated,
    PaymentConfirmed,
    PaymentFailed,
    PaymentOverdueWarning,   // day 4-7
    PaymentOverdueFinal,     // beyond day 7 (+ email)
    AccountSuspended,
    AccountReactivated,
    MaterialUploaded,
    AssignmentReminder,      // 3 days before deadline
    AssignmentOverdue,
    AssignmentGraded,
    AnnouncementPosted,
    ScheduleChange,
    EnrollmentApproved,
    EnrollmentRejected,
    ChatEscalation,
    SubjectAddCharge,
    SubjectDropCredit,
    NewMessage
}

public enum UserRole
{
    Admin,
    Guardian,
    Student,
    Tutor
}
