using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed record AdminWithdrawalPreview(string UserId, int EnrollmentId, DateTime EffectiveDate, decimal Refund, DateTime ExpiresAtUtc);
public sealed class AdminWithdrawalConfirmation(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("EduSmart.AdminWithdrawal.v1");
    public string Issue(string userId, int enrollmentId, DateTime date, decimal refund) => protector.Protect(JsonSerializer.Serialize(new AdminWithdrawalPreview(userId, enrollmentId, date, refund, DateTime.UtcNow.AddMinutes(20))));
    public AdminWithdrawalPreview? Read(string? token, string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(token) || token.Length > 5000) return null;
            var data = JsonSerializer.Deserialize<AdminWithdrawalPreview>(protector.Unprotect(token));
            return data != null && data.UserId == userId && data.ExpiresAtUtc > DateTime.UtcNow ? data : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }
}
