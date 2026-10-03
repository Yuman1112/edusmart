using Microsoft.AspNetCore.DataProtection;

namespace SecureSmartTuitionSystem.Services.Communication
{
    public sealed class MessageProtectionService
        : IMessageProtectionService
    {
        private readonly IDataProtector _textProtector;
        private readonly IDataProtector _fileProtector;

        public MessageProtectionService(
            IDataProtectionProvider provider)
        {
            _textProtector = provider.CreateProtector(
                "EduSmart.Communication.Message.v1");
            _fileProtector = provider.CreateProtector(
                "EduSmart.Communication.Attachment.v1");
        }

        public string ProtectText(string value) =>
            _textProtector.Protect(value);

        public string UnprotectText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                return _textProtector.Unprotect(value);
            }
            catch
            {
                return "[Message unavailable]";
            }
        }

        public byte[] ProtectFile(byte[] value) =>
            _fileProtector.Protect(value);

        public byte[] UnprotectFile(byte[] value) =>
            _fileProtector.Unprotect(value);
    }
}
