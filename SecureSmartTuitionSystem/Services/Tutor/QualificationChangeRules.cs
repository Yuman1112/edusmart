namespace SecureSmartTuitionSystem.Services.Tutor;
public static class QualificationChangeRules
{
    public static string Normalize(string? value) => (value ?? "").Trim();
    public static bool Same(string? before, string? after) => string.Equals(Normalize(before), Normalize(after), StringComparison.Ordinal);
}
