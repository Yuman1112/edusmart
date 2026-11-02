using SecureSmartTuitionSystem.Models.Security;
namespace SecureSmartTuitionSystem.ViewModels.Security;
public sealed class SecurityDetailsViewModel
{
    public AuditLogEntry Entry { get; set; } = new();
    public bool IntegrityValid { get; set; }
    public string Summary { get; set; } = "";
    public bool AIConfigured { get; set; }
    public string? AIExplanation { get; set; }
    public bool GeneratedByAI { get; set; }
    public string? ReturnUrl { get; set; }
}
