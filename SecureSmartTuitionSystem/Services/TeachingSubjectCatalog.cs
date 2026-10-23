
using System;
using System.Linq;

namespace SecureSmartTuitionSystem.Services;

public static class TeachingSubjectCatalog
{
    // ==========================================
    // 1. Academic Levels
    // ==========================================

    public static readonly string[] PrimaryLevels =
    {
        "YEAR 1",
        "YEAR 2",
        "YEAR 3",
        "YEAR 4",
        "YEAR 5",
        "YEAR 6"
    };

    public static readonly string[] SecondaryLevels =
    {
        "FORM 1",
        "FORM 2",
        "FORM 3",
        "FORM 4",
        "FORM 5"
    };

    // ==========================================
    // 2. Subjects by Academic Level
    // ==========================================

    // Primary: YEAR 1 - YEAR 3
    private static readonly string[] YearOneToThreeSubjects =
    {
        "BC",
        "BMP",
        "BIP",
        "MM",
        "SCI"
    };

    // Primary: YEAR 4 - YEAR 6
    private static readonly string[] YearFourToSixSubjects =
    {
        "BC",
        "BMP",
        "BIP",
        "MM",
        "SCI",
        "SEJ"
    };

    // Secondary: FORM 1 - FORM 3
    private static readonly string[] FormOneToThreeSubjects =
    {
        "BC",
        "BMK",
        "BIK",
        "MM",
        "SCI",
        "SEJ",
        "GEO",
        "RBT",
        "ASK"
    };

    // Secondary: FORM 4 - FORM 5
    private static readonly string[] FormFourToFiveSubjects =
    {
        "BC",
        "BMK",
        "BIK",
        "MM",
        "AM",
        "BIO",
        "CHE",
        "PHY",
        "SEJ",
        "ACC",
        "EKO",
        "PER"
    };

    // ==========================================
    // 3. Subject Display Order
    // ==========================================

    // Keep all supported subject codes.
    public static readonly string[] SubjectDisplayOrder =
    {
        "BC",
        "BMP",
        "BMK",
        "BIP",
        "BIK",
        "MM",
        "SCI",
        "SEJ",
        "GEO",
        "RBT",
        "ASK",
        "AM",
        "BIO",
        "CHE",
        "PHY",
        "ACC",
        "EKO",
        "PER"
    };

    // ==========================================
    // 4. Get Academic Levels by Education Stage
    // ==========================================

    public static string[] GetLevelsForStage(string? stage)
    {
        return stage?.Trim().ToUpperInvariant() switch
        {
            "PRIMARY" => PrimaryLevels,
            "SECONDARY" => SecondaryLevels,
            _ => Array.Empty<string>()
        };
    }

    // ==========================================
    // 5. Get Education Stage by Academic Level
    // ==========================================

    public static string? GetStageForLevel(string? level)
    {
        string normalizedLevel =
            level?.Trim().ToUpperInvariant() ?? "";

        if (PrimaryLevels.Contains(normalizedLevel))
        {
            return "Primary";
        }

        if (SecondaryLevels.Contains(normalizedLevel))
        {
            return "Secondary";
        }

        return null;
    }

    // ==========================================
    // 6. Get Subjects by Academic Level
    // ==========================================

    public static string[] GetSubjectsForLevel(string? level)
    {
        return level?.Trim().ToUpperInvariant() switch
        {
            "YEAR 1" or "YEAR 2" or "YEAR 3"
                => YearOneToThreeSubjects,

            "YEAR 4" or "YEAR 5" or "YEAR 6"
                => YearFourToSixSubjects,

            "FORM 1" or "FORM 2" or "FORM 3"
                => FormOneToThreeSubjects,

            "FORM 4" or "FORM 5"
                => FormFourToFiveSubjects,

            _ => Array.Empty<string>()
        };
    }

    // ==========================================
    // 7. Validate Education Stage & Academic Level
    // ==========================================

    public static bool IsValidStageAndLevel(
        string? stage,
        string? level)
    {
        string normalizedLevel =
            level?.Trim().ToUpperInvariant() ?? "";

        return GetLevelsForStage(stage)
            .Contains(normalizedLevel);
    }

    // ==========================================
    // 8. Validate Subject for Academic Level
    // ==========================================

    public static bool IsValidSubject(
        string? level,
        string? subjectCode)
    {
        string normalizedSubject =
            subjectCode?.Trim().ToUpperInvariant() ?? "";

        return GetSubjectsForLevel(level)
            .Contains(normalizedSubject);
    }


    public static string GetSubjectDisplayName(string? subjectCode)
    {
        return subjectCode?.Trim().ToUpperInvariant() switch
        {
            "BC" => "Chinese",
            "BMP" => "Bahasa Melayu (Primary)",
            "BMK" => "Bahasa Melayu (Secondary)",
            "BIP" => "English (Primary)",
            "BIK" => "English (Secondary)",
            "MM" => "Mathematics",
            "SCI" => "Science",
            "SEJ" => "Sejarah",
            "GEO" => "Geography",
            "RBT" => "Design and Technology",
            "ASK" => "Basic Computer Science",
            "AM" => "Additional Mathematics",
            "BIO" => "Biology",
            "CHE" => "Chemistry",
            "PHY" => "Physics",
            "ACC" => "Accounting",
            "EKO" => "Economics",
            "PER" => "Perniagaan",
            _ => subjectCode ?? ""
        };
    }
}
