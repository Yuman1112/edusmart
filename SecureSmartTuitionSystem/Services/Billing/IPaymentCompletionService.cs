namespace SecureSmartTuitionSystem.Services.Billing
{
    public interface IPaymentCompletionService
    {
        Task<PaymentCompletionResult>
            CompleteStripePaymentAsync(
                string checkoutSessionId,
                string? paymentIntentId,
                string stripeEventId,
                long amountPaidInSen,
                string currency,
                CancellationToken cancellationToken = default);
    }

    public class PaymentCompletionResult
    {
        public bool AlreadyProcessed { get; set; }

        public int PaymentId { get; set; }

        public string PaymentNumber { get; set; }
            = string.Empty;

        public int BillId { get; set; }

        public string BillNumber { get; set; }
            = string.Empty;

        public int EnrollmentApplicationId { get; set; }

        public string ApplicationNumber { get; set; }
            = string.Empty;

        public string ParentUserName { get; set; }
            = string.Empty;

        public string StudentUserName { get; set; }
            = string.Empty;

        public int CreatedEnrollmentCount { get; set; }
    }
}