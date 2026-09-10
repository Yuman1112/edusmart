namespace SecureSmartTuitionSystem.Models.Billing
{
    public enum BillStatus
    {
        PendingPayment = 0,
        Paid = 1,
        Overdue = 2,
        Cancelled = 3,
        Refunded = 4
    }
}