using System.Text.RegularExpressions;
namespace SecureSmartTuitionSystem.Services.Enrollment;

// Display normalization only: tutor account identifiers remain available in tutor/account screens.
public static class ClassNameFormatter
{
    public static string Clean(string? value) => Regex.Replace(value ?? "",
        @"(?:\s*·\s*|\s+-\s+)TU[0-9]+\b", "", RegexOptions.IgnoreCase).Trim();
}
