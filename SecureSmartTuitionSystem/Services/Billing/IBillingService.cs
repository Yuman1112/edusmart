using SecureSmartTuitionSystem.Models.Billing;

namespace SecureSmartTuitionSystem.Services.Billing
{
    public interface IBillingService
    {
        Task<Bill> GenerateBillAsync(
            int enrollmentApplicationId,
            CancellationToken cancellationToken = default);

        Task<Bill?> GetBillByApplicationIdAsync(
            int enrollmentApplicationId,
            CancellationToken cancellationToken = default);
    }
}