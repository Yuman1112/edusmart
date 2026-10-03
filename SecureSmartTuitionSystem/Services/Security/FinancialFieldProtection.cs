using Microsoft.AspNetCore.DataProtection;

namespace SecureSmartTuitionSystem.Services.Security;

public sealed class FinancialFieldProtection
{
    private const string Prefix = "edusmart:v1:";
    private readonly IDataProtector _protector;
    public FinancialFieldProtection(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("EduSmart.Financial.Notes.v1");
    public string Encrypt(string value) => Prefix + _protector.Protect(value);
    public string Decrypt(string value) => value.StartsWith(Prefix, StringComparison.Ordinal)
        ? _protector.Unprotect(value[Prefix.Length..]) : value;
    // Legacy plaintext remains readable until the Admin backfill operation encrypts it.
    // Invalid ciphertext throws; it is never displayed or treated as plaintext.
}
