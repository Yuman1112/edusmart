namespace SecureSmartTuitionSystem.Services;

public class GuardianMatchResult
{
    public bool IsExistingGuardianMatch { get; set; }
    public int? MatchedGuardianId { get; set; }
}

public class StudentMatchCandidate
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = null!;
    public decimal Score { get; set; }
}

public class StudentDuplicateCheckResult
{
    public bool IsFlagged { get; set; }
    public StudentMatchCandidate? BestMatch { get; set; }
}

/// <summary>
/// Implements the two-stage hybrid duplicate-record validation adopted in
/// Chapter 2.2 ("Duplicate Record Validation Techniques", Class &amp;
/// Enrollment Management Module):
///   1. Exact match on Guardian email / last-4 phone-or-IC digits.
///   2. Birthdate-based blocking, then Jaro-Winkler similarity (threshold
///      0.85) between the submitted student name and existing students
///      sharing that birthdate.
/// </summary>
public interface IDuplicateRecordService
{
    /// <summary>Exact-match check (Ch.2.2 "Exact Matching") against the Guardian table.</summary>
    Task<GuardianMatchResult> CheckExistingGuardianAsync(string email, string phoneOrIcLast4);

    /// <summary>Birthdate-blocked Jaro-Winkler check (Ch.2.2 "Jaro-Winkler Similarity" + "Adopted Approach").</summary>
    Task<StudentDuplicateCheckResult> CheckDuplicateStudentAsync(string fullName, DateOnly birthDate);

    /// <summary>Levenshtein edit distance between two strings (Ch.2.2 "Levenshtein Distance"), exposed for the administrator's manual-review screen.</summary>
    int LevenshteinDistance(string s, string t);

    /// <summary>Jaro-Winkler similarity in [0,1] between two strings (Ch.2.2 "Jaro-Winkler Similarity").</summary>
    decimal JaroWinklerSimilarity(string s1, string s2);
}
