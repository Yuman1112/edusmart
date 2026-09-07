namespace SecureSmartTuitionSystem.Services;

/// <summary>
/// AES-256 encryption applied at the application layer to specific Payment
/// and Invoice columns before Entity Framework Core persists them into SQL
/// Server, per Chapter 2.3 "Adopted Approach and Implementation" (Payment
/// &amp; Billing Module) and Chapter 2.4 (Database Integration).
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypts plaintext with AES-256-CBC and returns a base64 string safe to store in an NVARCHAR column.</summary>
    string Encrypt(string plainText);

    /// <summary>Reverses <see cref="Encrypt"/>. Returns an empty string for null/empty input rather than throwing.</summary>
    string Decrypt(string cipherTextBase64);
}
