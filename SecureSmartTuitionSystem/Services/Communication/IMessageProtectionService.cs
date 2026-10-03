namespace SecureSmartTuitionSystem.Services.Communication
{
    public interface IMessageProtectionService
    {
        string ProtectText(string value);
        string UnprotectText(string? value);
        byte[] ProtectFile(byte[] value);
        byte[] UnprotectFile(byte[] value);
    }
}
