using SecureSmartTuitionSystem.Models.Security;
namespace SecureSmartTuitionSystem.ViewModels.Security;
public sealed class SecurityIndexViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? UserId { get; set; }
    public string? EventName { get; set; }
    public SecuritySeverity? Severity { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public int RecentAlerts { get; set; }
    public bool EmailAlertsConfigured { get; set; }
    public List<SecurityLogRow> Rows { get; set; } = new();
}
public sealed class SecurityLogRow
{
    public AuditLogEntry Entry { get; set; } = new();
    public bool IntegrityValid { get; set; }
}
