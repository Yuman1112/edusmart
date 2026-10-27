using System.Text;

namespace SecureSmartTuitionSystem.Services.Enrollment;

/// <summary>
/// Name similarity is a review signal, never proof of identity or authority.
/// Report 1 section 2.2.5 specifies a 0.85 Jaro-Winkler threshold.
/// </summary>
public static class StudentNameSimilarity
{
    public const double ReviewThreshold = 0.85;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        // Normalize compatibility forms (e.g. full-width Latin letters), case
        // and whitespace; preserve punctuation and diacritics in people's names.
        return string.Join(" ", value.Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static double Score(string? first, string? second)
    {
        string left = Normalize(first);
        string right = Normalize(second);
        // Empty names must never become a positive duplicate signal.
        if (left.Length == 0 || right.Length == 0) return 0;
        if (left == right) return 1;

        int window = Math.Max(0, Math.Max(left.Length, right.Length) / 2 - 1);
        var leftMatched = new bool[left.Length];
        var rightMatched = new bool[right.Length];
        int matches = 0;

        for (int i = 0; i < left.Length; i++)
        {
            int start = Math.Max(0, i - window);
            int end = Math.Min(right.Length, i + window + 1);
            for (int j = start; j < end; j++)
            {
                if (rightMatched[j] || left[i] != right[j]) continue;
                leftMatched[i] = true;
                rightMatched[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0;
        int mismatches = 0;
        int rightIndex = 0;
        for (int i = 0; i < left.Length; i++)
        {
            if (!leftMatched[i]) continue;
            while (!rightMatched[rightIndex]) rightIndex++;
            if (left[i] != right[rightIndex]) mismatches++;
            rightIndex++;
        }

        double jaro = ((double)matches / left.Length +
                       (double)matches / right.Length +
                       (matches - mismatches / 2.0) / matches) / 3.0;
        // Conventional Winkler prefix boost only for an already close Jaro match.
        if (jaro <= 0.7) return jaro;
        int prefix = 0;
        while (prefix < Math.Min(4, Math.Min(left.Length, right.Length)) &&
               left[prefix] == right[prefix]) prefix++;
        return jaro + prefix * 0.1 * (1 - jaro);
    }
}
