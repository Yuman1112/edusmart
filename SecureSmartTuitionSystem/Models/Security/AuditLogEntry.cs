using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Security;

public enum SecuritySeverity { Information, Warning, High, Critical }

public sealed class AuditLogEntry
{
    public long AuditLogEntryId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? ActorUserId { get; set; }
    [MaxLength(256)] public string ActorName { get; set; } = "System";
    [MaxLength(100)] public string EventName { get; set; } = string.Empty;
    public SecuritySeverity Severity { get; set; }
    [MaxLength(120)] public string EntityName { get; set; } = string.Empty;
    [MaxLength(256)] public string EntityKey { get; set; } = string.Empty;
    [MaxLength(64)] public string RemoteIp { get; set; } = string.Empty;
    [MaxLength(120)] public string TraceId { get; set; } = string.Empty;
    [MaxLength(1000)] public string Summary { get; set; } = string.Empty;
    public string IntegrityProof { get; set; } = string.Empty;
}

public sealed class SecurityEmailDelivery
{
    public long AuditLogEntryId { get; set; }
    public DateTime SentAtUtc { get; set; }
}
