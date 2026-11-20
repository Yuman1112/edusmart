using System.Security.Cryptography;
using System.Text;

namespace SecureSmartTuitionSystem.Services.Security
{
    public static class PaymentAccessTokenService
    {
        public static bool IsWellFormed([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? token) => token is { Length: 64 } &&
            token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

        public static string GenerateToken()
        {
            byte[] tokenBytes =
                RandomNumberGenerator.GetBytes(32);

            return Convert
                .ToHexString(tokenBytes)
                .ToLowerInvariant();
        }

        public static string HashToken(
            string token)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);

            byte[] tokenBytes =
                Encoding.UTF8.GetBytes(token.Trim());

            byte[] hashBytes =
                SHA256.HashData(tokenBytes);

            return Convert
                .ToHexString(hashBytes)
                .ToLowerInvariant();
        }
    }
}
