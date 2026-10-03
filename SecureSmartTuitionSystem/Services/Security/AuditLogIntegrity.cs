using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SecureSmartTuitionSystem.Models.Security;

namespace SecureSmartTuitionSystem.Services.Security;

public sealed class AuditLogIntegrity
{
    private readonly IDataProtector _protector;
    public AuditLogIntegrity(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("EduSmart.Security.AuditIntegrity.v1");

    private static byte[] Digest(AuditLogEntry entry) => SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            OccurredAtTicks = entry.OccurredAtUtc.Ticks, entry.ActorUserId, entry.ActorName,
            entry.EventName, entry.Severity, entry.EntityName, entry.EntityKey,
            entry.RemoteIp, entry.TraceId, entry.Summary
        })));

    public void Seal(AuditLogEntry entry) =>
        entry.IntegrityProof = Convert.ToBase64String(_protector.Protect(Digest(entry)));

    public bool Verify(AuditLogEntry entry)
    {
        try
        {
            byte[] expected = _protector.Unprotect(Convert.FromBase64String(entry.IntegrityProof));
            return CryptographicOperations.FixedTimeEquals(expected, Digest(entry));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }
}
