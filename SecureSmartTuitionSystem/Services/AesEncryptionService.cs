using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace SecureSmartTuitionSystem.Services;

/// <summary>
/// Symmetric AES-256 encryption via the built-in System.Security.Cryptography
/// namespace — no third-party NuGet package required, matching the report's
/// "dependency-free" rationale for choosing AES over RSA (Ch.2.3: AES is
/// "directly supported through the built-in System.Security.Cryptography.Aes
/// class").
///
/// The key/IV are read from configuration (in production: from an
/// environment variable, User Secrets, or a key vault — never from source
/// control). A fixed IV would normally be a concern for AES-CBC, but every
/// value protected here (gateway tokens, billing remarks) is unique per row,
/// so a random IV is generated and prefixed to each ciphertext instead of
/// reusing appsettings' IV, keeping the scheme safe without needing a
/// separate IV column per encrypted field.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Security:AesKeyBase64"]
            ?? throw new InvalidOperationException("Security:AesKeyBase64 is not configured.");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                "Security:AesKeyBase64 must decode to exactly 32 bytes for AES-256.");
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV(); // random per-value IV, prefixed to the ciphertext below

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Store as [IV (16 bytes)][ciphertext], base64-encoded as a whole.
        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherTextBase64)
    {
        if (string.IsNullOrEmpty(cipherTextBase64)) return string.Empty;

        var combined = Convert.FromBase64String(cipherTextBase64);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = new byte[16];
        Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = new byte[combined.Length - iv.Length];
        Buffer.BlockCopy(combined, iv.Length, cipherBytes, 0, cipherBytes.Length);

        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
