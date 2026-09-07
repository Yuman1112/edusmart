using Microsoft.AspNetCore.Http;

namespace SecureSmartTuitionSystem.Services;

public class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// System Security Module: server-side file type/extension/size validation
/// (Ch.2.5: "File type, extension, and size are validated on the server
/// side before any uploaded file is stored"). Applied to message
/// attachments (Table 4.15) and any other upload surface.
/// </summary>
public interface IFileValidationService
{
    FileValidationResult Validate(IFormFile file);
}
