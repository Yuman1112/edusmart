using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.Services.Security;

namespace SecureSmartTuitionSystem.Services.Tutor;

public static class TutorQualificationService
{
    public static async Task<(MaterialFile? File, string? Error)> ReadDocumentAsync(
        IFormFile? upload, CancellationToken cancellationToken)
    {
        if (upload == null)
            return (null, null);

        if (MaterialFileService.PreviewContentType(MaterialFileService.SafeName(upload.FileName)) == null)
            return (null, "Choose a PDF, PNG or JPG file.");

        try
        {
            return await MaterialFileService.ReadAsync(upload, cancellationToken);
        }
        catch (IOException)
        {
            return (null, "The file could not be read. Select it again and retry.");
        }
    }

    public static async Task<string?> FindRemovalConflictAsync(
        ApplicationDbContext db, string userId, IEnumerable<TutorTeachingSubject> removed,
        CancellationToken cancellationToken)
    {
        var removedSubjects = removed.ToList();
        if (removedSubjects.Count == 0)
            return null;

        var today = ClassScheduleService.Today;
        var sessions = await db.ClassSessions.AsNoTracking()
            .Where(session => session.TutorUserId == userId
                && session.Status == ClassSessionStatus.Active
                && (session.EndDate == null || session.EndDate >= today))
            .OrderBy(session => session.ClassCode)
            .Select(session => new
            {
                session.ClassCode,
                session.StartDate,
                session.CreatedAt,
                session.Subject.EducationStage,
                session.Subject.AcademicLevel,
                session.Subject.SubjectName
            })
            .ToListAsync(cancellationToken);

        var affected = sessions
            .Where(session => AcademicYearPolicy.EndOfYear(session.StartDate ?? session.CreatedAt.AddHours(8)) >= today
                && TutorTeachingEligibilityService.Matches(removedSubjects,
                session.EducationStage, session.AcademicLevel, session.SubjectName))
            .Select(session => session.ClassCode)
            .Distinct()
            .Take(6)
            .ToList();

        return affected.Count == 0 ? null :
            "Reassign or close these active classes before removing the teaching approval: "
            + string.Join(", ", affected.Take(5)) + (affected.Count > 5 ? ", and others." : ".");
    }

    public static bool SameApproval(TutorTeachingSubject subject, string? level, string? code) =>
        string.Equals(subject.AcademicLevel?.Trim(), level, StringComparison.OrdinalIgnoreCase)
        && string.Equals(subject.SubjectCode?.Trim(), code, StringComparison.OrdinalIgnoreCase);

    public static void AddAudit(
        ApplicationDbContext db, AuditLogIntegrity integrity, HttpContext context,
        string userId, string summary)
    {
        var entry = new AuditLogEntry
        {
            ActorUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = SecurityEventService.Limit(context.User.Identity?.Name ?? "Administrator", 256),
            EventName = "TutorTeachingApprovalsChanged",
            EntityName = nameof(TutorProfile),
            EntityKey = SecurityEventService.Limit(userId, 256),
            Severity = SecuritySeverity.Information,
            RemoteIp = SecurityEventService.Limit(context.Connection.RemoteIpAddress?.ToString() ?? "", 64),
            TraceId = SecurityEventService.Limit(context.TraceIdentifier, 120),
            Summary = SecurityEventService.Limit(summary, 1000)
        };
        integrity.Seal(entry);
        db.AuditLogEntries.Add(entry);
    }

    public static TutorQualificationDocument CreateDocument(
        TutorProfile profile, TutorDocumentKind kind, MaterialFile file) => new()
    {
        TutorProfile = profile,
        Kind = kind,
        FileName = file.Name,
        Content = file.Content
    };
}
