namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public enum EnrollmentStatus
    {
        PendingReview,
        DuplicateReview,
        ApprovedPendingPayment,
        Rejected,
        PaymentConfirmed,
        Enrolled,
        Cancelled
    }
}