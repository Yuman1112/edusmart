using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using System.Security.Cryptography;
namespace SecureSmartTuitionSystem.Services.Security;
public sealed class PaymentCallbackTokenService
{
    private readonly IDataProtector _protector;
    public PaymentCallbackTokenService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("EduSmart.Payment.Callback.v1");
    public string Create(int paymentId) => _protector.Protect(
        paymentId.ToString(CultureInfo.InvariantCulture) + ":" +
        DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
    public bool TryRead(string? token, out int paymentId)
    {
        paymentId = 0;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2000) return false;
        try
        {
            string[] parts = _protector.Unprotect(token).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out paymentId) && paymentId > 0 &&
                long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long expiry) &&
                expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch (CryptographicException) { return false; }
    }
}
