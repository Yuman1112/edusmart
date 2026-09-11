namespace SecureSmartTuitionSystem.Settings
{
    public class StripeSettings
    {
        public const string SectionName = "Stripe";

        public string SecretKey { get; set; } = string.Empty;

        public string WebhookSecret { get; set; } = string.Empty;
    }
}