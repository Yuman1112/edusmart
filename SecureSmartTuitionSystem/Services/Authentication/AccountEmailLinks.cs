using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
namespace SecureSmartTuitionSystem.Services.Authentication;
public sealed class AccountEmailLinks
{
    public sealed record Change(Guid RequestId, Guid Nonce, string IdentityToken);
    public sealed record Recovery(string UserId, string IdentityToken, string RecipientHash);
    private readonly ITimeLimitedDataProtector _change;
    private readonly ITimeLimitedDataProtector _recovery;
    public AccountEmailLinks(IDataProtectionProvider provider)
    {
        _change = provider.CreateProtector("EduSmart.AccountEmail.Change.v1").ToTimeLimitedDataProtector();
        _recovery = provider.CreateProtector("EduSmart.AccountEmail.Recovery.v1").ToTimeLimitedDataProtector();
    }
    public string ProtectChange(Change value, DateTime expiresUtc) => _change.Protect(JsonSerializer.Serialize(value), new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc)));
    public Change? ReadChange(string? token) => Read<Change>(_change, token);
    public string ProtectRecovery(Recovery value) => "recovery.v1." + _recovery.Protect(JsonSerializer.Serialize(value), TimeSpan.FromHours(1));
    public Recovery? ReadRecovery(string? token) => token?.StartsWith("recovery.v1.", StringComparison.Ordinal) == true ? Read<Recovery>(_recovery, token[12..]) : null;
    private static T? Read<T>(ITimeLimitedDataProtector protector, string? value) where T : class
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 8192) return null;
        try { return JsonSerializer.Deserialize<T>(protector.Unprotect(value)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or ArgumentException) { return null; }
    }
}
