using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.Services.Enrollment;

public sealed record StudentDuplicateCandidate(
    string Source, int RecordId, string Reference, string StudentName,
    DateTime DateOfBirth, double Similarity);

/// <summary>
/// Birthdate blocking runs in SQL; Jaro-Winkler runs only on those candidates.
/// Only the Admin controller may expose results to a browser. Public applicants
/// receive no other family's personal information or record existence details.
/// </summary>
public sealed class StudentDuplicateReviewService(ApplicationDbContext db)
{
    // A changed candidate set requires a fresh review rather than accepting a
    // checkbox from a browser page that no longer reflects the database.
    public static string ReviewVersion(IReadOnlyList<StudentDuplicateCandidate> candidates) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(candidates)));

    public static bool IsReviewComplete(IReadOnlyList<StudentDuplicateCandidate> candidates,
        bool confirmed, string? version, string? reason)
    {
        if (candidates.Count == 0) return true;
        int length = reason?.Trim().Length ?? 0;
        return confirmed && version == ReviewVersion(candidates) && length is >= 10 and <= 200;
    }

    public async Task<List<StudentDuplicateCandidate>> FindAsync(
        string name, DateTime birthdate, string identificationNumber,
        int? knownStudentProfileId = null, int? excludedApplicationId = null,
        CancellationToken cancellationToken = default)
    {
        var day = birthdate.Date;
        var nextDay = day.AddDays(1);
        var students = await db.StudentProfiles.AsNoTracking()
            .Where(x => x.DateOfBirth >= day && x.DateOfBirth < nextDay &&
                (!knownStudentProfileId.HasValue || x.StudentProfileId != knownStudentProfileId.Value))
            .Select(x => new { x.StudentProfileId, x.StudentNumber, x.FullName,
                x.DateOfBirth, x.IdentificationNumber })
            .ToListAsync(cancellationToken);

        // Include unresolved applications, so two applicants cannot avoid review
        // merely because neither has a StudentProfile yet. Enrolled applications
        // are represented by their StudentProfile rather than shown twice.
        var applications = await db.EnrollmentApplications.AsNoTracking()
            .Where(x => x.StudentDateOfBirth >= day && x.StudentDateOfBirth < nextDay &&
                (!excludedApplicationId.HasValue || x.EnrollmentApplicationId != excludedApplicationId.Value) &&
                (x.Status == EnrollmentStatus.PendingReview ||
                 x.Status == EnrollmentStatus.ApprovedPendingPayment ||
                 x.Status == EnrollmentStatus.PaymentConfirmed))
            .Select(x => new { x.EnrollmentApplicationId, x.ApplicationNumber,
                x.StudentName, x.StudentDateOfBirth, x.StudentIdentificationNumber })
            .ToListAsync(cancellationToken);

        var candidates = new List<StudentDuplicateCandidate>();
        string identity = StudentNameSimilarity.Normalize(identificationNumber);
        foreach (var student in students)
        {
            // Existing exact-ID checks already handle a known student adding a
            // subject. Similarity must not block that legitimate workflow.
            if (identity.Length > 0 && StudentNameSimilarity.Normalize(student.IdentificationNumber) == identity) continue;
            double score = StudentNameSimilarity.Score(name, student.FullName);
            if (score >= StudentNameSimilarity.ReviewThreshold)
                candidates.Add(new("Student profile", student.StudentProfileId,
                    student.StudentNumber, student.FullName, student.DateOfBirth, score));
        }
        foreach (var application in applications)
        {
            if (identity.Length > 0 && StudentNameSimilarity.Normalize(application.StudentIdentificationNumber) == identity) continue;
            double score = StudentNameSimilarity.Score(name, application.StudentName);
            if (score >= StudentNameSimilarity.ReviewThreshold)
                candidates.Add(new("Enrollment application", application.EnrollmentApplicationId,
                    application.ApplicationNumber, application.StudentName, application.StudentDateOfBirth, score));
        }
        return candidates.OrderByDescending(x => x.Similarity)
            .ThenBy(x => x.Source, StringComparer.Ordinal)
            .ThenBy(x => x.RecordId).ToList();
    }
}
