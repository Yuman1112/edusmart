using Microsoft.AspNetCore.Http;

namespace SecureSmartTuitionSystem.Services;

public class FileValidationService : IFileValidationService
{
    // Server-side allow-list; never trust the client-supplied Content-Type alone.
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".docx", ".xlsx"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public FileValidationResult Validate(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return new FileValidationResult { IsValid = false, ErrorMessage = "No file was provided." };
        }

        if (file.Length > MaxFileSizeBytes)
        {
            // Table 4.15, Alternative Flow A2
            return new FileValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid attachment format or file size exceeds maximum limit."
            };
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return new FileValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid attachment format or file size exceeds maximum limit."
            };
        }

        return new FileValidationResult { IsValid = true };
    }
}
