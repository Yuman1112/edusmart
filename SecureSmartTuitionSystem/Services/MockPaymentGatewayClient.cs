namespace SecureSmartTuitionSystem.Services;

/// <summary>
/// Development/demo stand-in for the real gateway. Swap for a real SDK
/// client (implementing <see cref="IPaymentGatewayClient"/>) in
/// Program.cs before going live — no other code in the system needs to
/// change, since Controllers and Services only depend on the interface.
/// </summary>
public class MockPaymentGatewayClient : IPaymentGatewayClient
{
    public Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request)
    {
        // Deliberately never inspects/stores request.RawPaymentDetails beyond this call.
        if (request.Amount <= 0)
        {
            return Task.FromResult(new GatewayChargeResult
            {
                IsSuccess = false,
                FailureReason = "Invalid amount."
            });
        }

        var token = $"tok_{Guid.NewGuid():N}";
        return Task.FromResult(new GatewayChargeResult
        {
            IsSuccess = true,
            GatewayToken = token
        });
    }
}
