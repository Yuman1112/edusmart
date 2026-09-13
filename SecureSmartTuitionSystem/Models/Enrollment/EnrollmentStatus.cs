namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public enum EnrollmentStatus
    {
        PendingReview = 0,
        ApprovedPendingPayment = 2,
        Rejected = 3,
        PaymentConfirmed = 4,
        Enrolled = 5,
        Cancelled = 6
    }
}
