using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;

namespace SecureSmartTuitionSystem.Services;

/// <summary>
/// See <see cref="IDuplicateRecordService"/> for the design rationale. Both
/// algorithms are implemented as a single, dependency-free C# method each
/// (Ch.2.2: "can be implemented as a single, dependency-free C# method...
/// since only basic string indexing and array operations are required").
/// </summary>
public class DuplicateRecordService : IDuplicateRecordService
{
    /// <summary>Jaro-Winkler score at/above which an existing student record is flagged for administrator review (Ch.2.2).</summary>
    public const decimal JaroWinklerFlagThreshold = 0.85m;

    private readonly AppDbContext _db;

    public DuplicateRecordService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GuardianMatchResult> CheckExistingGuardianAsync(string email, string phoneOrIcLast4)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Indexed WHERE clause comparison -> SQL Server performs an index seek (Ch.2.2 "Exact Matching")
        var existing = await _db.Guardians
            .Where(g => g.Email.ToLower() == normalizedEmail)
            .Select(g => new { g.GuardianId, g.IdentificationNo, g.PhoneNumber })
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            return new GuardianMatchResult { IsExistingGuardianMatch = false };
        }

        var storedLast4 = (existing.IdentificationNo ?? existing.PhoneNumber);
        storedLast4 = storedLast4.Length >= 4 ? storedLast4[^4..] : storedLast4;

        var isMatch = string.Equals(storedLast4, phoneOrIcLast4?.Trim(), StringComparison.OrdinalIgnoreCase);

        return new GuardianMatchResult
        {
            IsExistingGuardianMatch = isMatch,
            MatchedGuardianId = isMatch ? existing.GuardianId : null
        };
    }

    public async Task<StudentDuplicateCheckResult> CheckDuplicateStudentAsync(string fullName, DateOnly birthDate)
    {
        // Stage 1 (blocking): restrict comparison set to students sharing the same birthdate,
        // narrowing candidate pairs before the more expensive similarity check (Ch.2.2 "Adopted Approach",
        // mirroring the filter-then-verify pattern of Yulianto & Nurhasanah 2021 / Dreßler & Ngonga Ngomo 2016).
        var candidates = await _db.Students
            .Where(s => s.BirthDate == birthDate)
            .Select(s => new { s.StudentId, s.FullName })
            .ToListAsync();

        StudentMatchCandidate? best = null;

        // Stage 2 (verify): Jaro-Winkler similarity against each blocked candidate.
        foreach (var candidate in candidates)
        {
            var score = JaroWinklerSimilarity(fullName, candidate.FullName);
            if (best is null || score > best.Score)
            {
                best = new StudentMatchCandidate
                {
                    StudentId = candidate.StudentId,
                    FullName = candidate.FullName,
                    Score = score
                };
            }
        }

        var isFlagged = best is not null && best.Score >= JaroWinklerFlagThreshold;

        return new StudentDuplicateCheckResult
        {
            IsFlagged = isFlagged,
            BestMatch = isFlagged ? best : null
        };
    }

    public int LevenshteinDistance(string s, string t)
    {
        s ??= string.Empty;
        t ??= string.Empty;

        var n = s.Length;
        var m = t.Length;
        var distance = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) distance[i, 0] = i;
        for (var j = 0; j <= m; j++) distance[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // deletion
                        distance[i, j - 1] + 1),     // insertion
                    distance[i - 1, j - 1] + cost);  // substitution
            }
        }

        return distance[n, m];
    }

    public decimal JaroWinklerSimilarity(string s1, string s2)
    {
        s1 = (s1 ?? string.Empty).Trim().ToLowerInvariant();
        s2 = (s2 ?? string.Empty).Trim().ToLowerInvariant();

        var jaro = JaroSimilarity(s1, s2);
        if (jaro == 0m) return 0m;

        // Common prefix, capped at 4 characters (Ch.2.2 "Jaro-Winkler Similarity")
        var maxPrefix = Math.Min(4, Math.Min(s1.Length, s2.Length));
        var prefixLength = 0;
        for (var i = 0; i < maxPrefix; i++)
        {
            if (s1[i] != s2[i]) break;
            prefixLength++;
        }

        const decimal scalingFactor = 0.1m; // conventional p = 0.1 (Ch.2.2)
        return jaro + prefixLength * scalingFactor * (1 - jaro);
    }

    private static decimal JaroSimilarity(string s1, string s2)
    {
        if (s1.Length == 0 && s2.Length == 0) return 1m;
        if (s1.Length == 0 || s2.Length == 0) return 0m;

        // Matching window, per the standard Jaro definition
        var matchDistance = Math.Max(0, Math.Max(s1.Length, s2.Length) / 2 - 1);

        var s1Matches = new bool[s1.Length];
        var s2Matches = new bool[s2.Length];

        var matches = 0;
        for (var i = 0; i < s1.Length; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(i + matchDistance + 1, s2.Length);

            for (var j = start; j < end; j++)
            {
                if (s2Matches[j] || s1[i] != s2[j]) continue;
                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0m;

        // Count transpositions
        var transpositions = 0;
        var k = 0;
        for (var i = 0; i < s1.Length; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) transpositions++;
            k++;
        }
        transpositions /= 2;

        var m = (decimal)matches;
        return (1m / 3m) * (m / s1.Length + m / s2.Length + (m - transpositions) / m);
    }
}
