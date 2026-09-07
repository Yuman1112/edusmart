namespace SecureSmartTuitionSystem.Services;

public class GatewayChargeRequest
{
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = null!; // Card | OnlineBanking | EWallet
    /// <summary>Raw card/bank details as entered by the payer. NEVER persisted — sent directly to the gateway and discarded.</summary>
    public string RawPaymentDetails { get; set; } = null!;
}

public class GatewayChargeResult
{
    public bool IsSuccess { get; set; }
    /// <summary>Non-sensitive, tokenised transaction reference returned by the gateway (this — not any card number — is what gets stored, AES-256 encrypted).</summary>
    public string? GatewayToken { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// Boundary to a third-party payment gateway. Per PCI-DSS-aligned
/// cardholder data protection (Ch.2.3): "actual cardholder data entry and
/// processing is delegated to a third-party payment gateway... by which
/// only a non-sensitive transaction reference token... is returned to and
/// stored within the system's own SQL Server database." This interface is
/// the seam a real gateway SDK (Stripe/Billplz/iPay88/etc.) plugs into; the
/// in-repo implementation is a stand-in that never touches real funds.
/// </summary>
public interface IPaymentGatewayClient
{
    Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request);
}
