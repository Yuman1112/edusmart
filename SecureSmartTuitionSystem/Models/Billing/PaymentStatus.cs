namespace SecureSmartTuitionSystem.Models.Billing
{
    public enum PaymentStatus
    {
        Pending = 0,
        Succeeded = 1,
        Failed = 2,
        Cancelled = 3,
        Refunded = 4
    }
}