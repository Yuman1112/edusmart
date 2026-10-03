using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class EnrollmentRulesService(ApplicationDbContext db)
{
    // A subject may be enrolled again in a new academic year, not duplicated within one year.
    public async Task<HashSet<int>> UnavailableSubjectsAsync(int studentId, int year, int? excludeApplicationId = null, CancellationToken ct = default)
    {
        var enrolled = await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.StudentProfileId == studentId && e.StartDate.Year == year &&
                (!excludeApplicationId.HasValue || e.EnrollmentApplicationId != excludeApplicationId.Value))
            .Select(e => e.ClassSession.SubjectId).ToListAsync(ct);
        var pending = await db.Set<EnrollmentApplicationSubject>().AsNoTracking()
            .Where(x => x.EnrollmentApplication.ExistingStudentProfileId == studentId &&
                x.EnrollmentApplication.RequestedStartDate.Year == year &&
                (!excludeApplicationId.HasValue || x.EnrollmentApplicationId != excludeApplicationId.Value) &&
                (x.EnrollmentApplication.Status == EnrollmentStatus.PendingReview ||
                 x.EnrollmentApplication.Status == EnrollmentStatus.ApprovedPendingPayment ||
                 x.EnrollmentApplication.Status == EnrollmentStatus.PaymentConfirmed))
            .Select(x => x.ClassSession.SubjectId).ToListAsync(ct);
        return enrolled.Concat(pending).ToHashSet();
    }
    public async Task<int> OccupiedSeatsAsync(int classId, int? excludeApplicationId = null, CancellationToken ct = default)
    {
        var today = ClassScheduleService.Today;
        int enrolled = await db.StudentEnrollments.CountAsync(e => e.ClassSessionId == classId &&
            (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop) &&
            e.StartDate.Year >= today.Year && (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate > today) && (!e.EndedAt.HasValue || e.EndedAt > today), ct);
        int reserved = await db.Set<EnrollmentApplicationSubject>().CountAsync(x => x.ClassSessionId == classId &&
            (!excludeApplicationId.HasValue || x.EnrollmentApplicationId != excludeApplicationId.Value) &&
            (x.EnrollmentApplication.Status == EnrollmentStatus.ApprovedPendingPayment || x.EnrollmentApplication.Status == EnrollmentStatus.PaymentConfirmed), ct);
        return enrolled + reserved;
    }
    public async Task<string?> ValidateAsync(List<int> ids, DateTime start, int? studentId = null, int? applicationId = null, CancellationToken ct = default, int? durationMonths = null)
    {
        var classes = await db.ClassSessions.AsNoTracking().Include(x => x.Subject).Where(x => ids.Contains(x.ClassSessionId)).ToListAsync(ct);
        if (classes.Count != ids.Distinct().Count() || classes.Count == 0) return "Select valid classes.";
        if (studentId.HasValue)
        {
            var unavailable = await UnavailableSubjectsAsync(studentId.Value, start.Year, applicationId, ct);
            if (classes.Any(c => unavailable.Contains(c.SubjectId)))
                return "This subject has already been enrolled or has a pending application for the selected academic year.";
        }
        if (classes.GroupBy(c => c.SubjectId).Any(g => g.Count() > 1)) return "Select only one class for each subject.";
        if (durationMonths.HasValue) { var planError = AcademicYearPolicy.Validate(start, durationMonths.Value, classes); if (planError != null) return planError; }
        foreach (var c in classes)
        {
            if (!c.Subject.IsActive) return $"Subject {c.Subject.SubjectName} is no longer available for enrollment.";
            if (!ClassScheduleService.ValidOn(c, start)) return $"Class {c.ClassName} is unavailable on the selected enrollment start date.";
            if (await OccupiedSeatsAsync(c.ClassSessionId, applicationId, ct) >= c.Capacity) return $"Class {c.ClassName} has reached its maximum capacity, including approved applications awaiting payment.";
        }
        var other = studentId.HasValue ? await db.StudentEnrollments.AsNoTracking().Include(x => x.ClassSession)
            .Where(x => x.StudentProfileId == studentId && x.StartDate.Year == start.Year && (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop) &&
                (!x.DropEffectiveDate.HasValue || x.DropEffectiveDate > start) && (!x.EndedAt.HasValue || x.EndedAt > start)).ToListAsync(ct) : new List<StudentEnrollment>();
        other = other.Where(e => AcademicYearPolicy.ClassEnd(e.ClassSession) >= start.Date).ToList();
        foreach (var c in classes)
        {
            if (other.Any(e => e.ClassSession.SubjectId == c.SubjectId)) return "The student is already enrolled in this subject. Withdraw the existing subject before starting another class for it.";
            if (other.Any(e => e.ClassSessionId == c.ClassSessionId)) return $"The student is already enrolled in {c.ClassName}.";
            foreach (var o in other.Where(e => e.ClassSession.Status == ClassSessionStatus.Active))
            {
                DateTime otherStart = o.StartDate > ClassScheduleService.FirstDate(o.ClassSession) ? o.StartDate : ClassScheduleService.FirstDate(o.ClassSession);
                DateTime? end = AcademicYearPolicy.ClassEnd(o.ClassSession);
                if (o.DropEffectiveDate.HasValue && (!end.HasValue || o.DropEffectiveDate <= end)) end = o.DropEffectiveDate.Value.AddDays(-1);
                if (o.EndedAt.HasValue && (!end.HasValue || o.EndedAt <= end)) end = o.EndedAt.Value.Date.AddDays(-1);
                if (ClassScheduleService.WeeklyOverlap(start, AcademicYearPolicy.ClassEnd(c), c.DayOfWeek, c.StartTime, c.EndTime,
                    otherStart, end, o.ClassSession.DayOfWeek, o.ClassSession.StartTime, o.ClassSession.EndTime)) return $"Class {c.ClassName} conflicts with the student's existing class {o.ClassSession.ClassName}.";
            }
            if (studentId.HasValue)
            {
                var classEnd = AcademicYearPolicy.ClassEnd(c);
                var replacements = await db.ClassLessonChanges.AsNoTracking().Where(x => x.ReplacementDate >= start &&
                    x.ReplacementDate <= classEnd && x.ReplacementStartTime < c.EndTime && x.ReplacementEndTime > c.StartTime).ToListAsync(ct);
                if (replacements.Any(x => x.ReplacementDate!.Value.DayOfWeek == c.DayOfWeek &&
                    (JsonSerializer.Deserialize<List<int>>(x.StudentIdsJson) ?? new()).Contains(studentId.Value))) return $"Class {c.ClassName} overlaps the student's replacement lesson.";
            }
            foreach (var otherClass in classes.Where(x => x.ClassSessionId > c.ClassSessionId))
                if (ClassScheduleService.WeeklyOverlap(start, AcademicYearPolicy.ClassEnd(c), c.DayOfWeek, c.StartTime, c.EndTime,
                    start, AcademicYearPolicy.ClassEnd(otherClass), otherClass.DayOfWeek, otherClass.StartTime, otherClass.EndTime)) return $"Classes {c.ClassName} and {otherClass.ClassName} overlap.";
        }
        // An approved request from the same student also reserves a future timetable.
        if (studentId.HasValue)
        {
            var pending = await db.EnrollmentApplications.AsNoTracking().Include(x => x.SelectedSubjects).ThenInclude(x => x.ClassSession)
                .Where(x => x.ExistingStudentProfileId == studentId && (x.Status == EnrollmentStatus.ApprovedPendingPayment || x.Status == EnrollmentStatus.PaymentConfirmed) &&
                    (!applicationId.HasValue || x.EnrollmentApplicationId != applicationId)).ToListAsync(ct);
            foreach (var c in classes) foreach (var app in pending) foreach (var item in app.SelectedSubjects)
                if (c.SubjectId == item.ClassSession.SubjectId && app.RequestedStartDate <= AcademicYearPolicy.ClassEnd(c) && start <= AcademicYearPolicy.ClassEnd(item.ClassSession))
                    return "The student's other approved application already reserves this subject.";
            foreach (var c in classes) foreach (var app in pending) foreach (var item in app.SelectedSubjects)
                if (ClassScheduleService.WeeklyOverlap(start, AcademicYearPolicy.ClassEnd(c), c.DayOfWeek, c.StartTime, c.EndTime,
                    app.RequestedStartDate, AcademicYearPolicy.ClassEnd(item.ClassSession), item.ClassSession.DayOfWeek, item.ClassSession.StartTime, item.ClassSession.EndTime))
                    return "The student's other approved application has an overlapping class.";
        }
        return null;
    }
}
