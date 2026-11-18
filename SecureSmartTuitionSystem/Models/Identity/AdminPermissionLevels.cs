namespace SecureSmartTuitionSystem.Models.Identity;

// Permission levels refine Admin account management; they are not Identity roles.
public static class AdminPermissionLevels
{
    public const string Standard = "Standard";
    public const string High = "High";

    public static bool IsHigh(string? level) =>
        string.Equals(level, High, StringComparison.Ordinal);
}
