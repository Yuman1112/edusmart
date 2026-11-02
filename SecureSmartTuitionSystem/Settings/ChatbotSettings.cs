namespace SecureSmartTuitionSystem.Settings;
public sealed class ChatbotSettings
{
    public const string SectionName = "Chatbot";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}
