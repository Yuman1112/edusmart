namespace SecureSmartTuitionSystem.Settings;

public sealed class SecuritySettings
{
    public const string SectionName = "Security";
    public string AdminAlertEmail { get; set; } = string.Empty;
    public int AlertIntervalMinutes { get; set; } = 5;
}
