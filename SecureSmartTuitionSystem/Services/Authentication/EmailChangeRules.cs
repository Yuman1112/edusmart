using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecureSmartTuitionSystem.Models.Identity;
namespace SecureSmartTuitionSystem.Services.Authentication;
public static class EmailChangeRules
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string RecipientHash(EmailOtpRecipient recipient) => Hash(JsonSerializer.Serialize(recipient));
    public static bool Live(EmailChangeRequest request, ApplicationUser user, DateTime now) =>
        request.Purpose == EmailChangePurpose.SelfService && request.UserId == user.Id && user.IsActive && !user.MustChangePassword && now < request.ExpiresAtUtc &&
        request.Status is EmailChangeStatus.VerifyCurrent or EmailChangeStatus.VerifyNew &&
        request.SecurityStampHash == Hash(user.SecurityStamp ?? "") &&
        string.Equals(request.OriginalEmail, user.Email, StringComparison.OrdinalIgnoreCase);
    public static bool CodeMatches(EmailChangeRequest request, string? code, DateTime now, string? expectedHash) =>
        request.Status == EmailChangeStatus.VerifyCurrent && request.Attempts < 5 && now < request.CodeExpiresAtUtc &&
        expectedHash?.Length == 64 && code?.Length == 6 && code.All(char.IsAsciiDigit) && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash), Encoding.ASCII.GetBytes(Hash(request.CodeNonce.ToString("N") + ":" + code)));
}
