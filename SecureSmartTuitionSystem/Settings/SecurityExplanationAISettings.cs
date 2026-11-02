namespace SecureSmartTuitionSystem.Settings;
public sealed class SecurityExplanationAISettings
{
    public const string SectionName = "SecurityExplanationAI";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}
