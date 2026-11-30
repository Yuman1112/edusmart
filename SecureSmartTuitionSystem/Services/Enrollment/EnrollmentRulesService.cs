using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class EnrollmentRulesService(ApplicationDbContext db)
{
    public async Task<HashSet<int>> UnavailableSubjectsAsync(int studentId, DateTime start, int? excludeApplicationId = null, CancellationToken ct = default, DateTime? end = null)
    {
        var snapshot = await EnrollmentAvailability.LoadAsync(db, [], studentId, excludeApplicationId, ct);
        var subjects = await db.StudentEnrollments.Where(x=>x.StudentProfileId==studentId).Select(x=>x.ClassSession.SubjectId)
            .Union(db.Set<EnrollmentApplicationSubject>().Where(x=>x.EnrollmentApplication.ExistingStudentProfileId==studentId).Select(x=>x.ClassSession.SubjectId)).Distinct().ToListAsync(ct);
        return subjects.Where(subject=>snapshot.SubjectBlocked(studentId,subject,start,end??AcademicYearPolicy.EndOfYear(start))).ToHashSet();
    }
    // Default is the maximum booked occupancy from today onward, also used when
    // an Admin reduces capacity. Enrollment callers pass the requested start.
    public async Task<int> OccupiedSeatsAsync(int classId, int? excludeApplicationId = null, CancellationToken ct = default, DateTime? startDate = null)
    {
        var c=await db.ClassSessions.AsNoTracking().SingleAsync(x=>x.ClassSessionId==classId,ct);
        var from=(startDate??ClassScheduleService.Today).Date;
        if(ClassScheduleService.FirstDate(c)>from)from=ClassScheduleService.FirstDate(c);
        var snapshot=await EnrollmentAvailability.LoadAsync(db,[classId],null,excludeApplicationId,ct);
        return snapshot.PeakSeats(classId,from,AcademicYearPolicy.ClassEnd(c));
    }
    public async Task<string?> ValidateAsync(List<int> ids, DateTime start, int? studentId = null, int? applicationId = null, CancellationToken ct = default, int? durationMonths = null)
    {
        var classes = await db.ClassSessions.AsNoTracking().Include(x => x.Subject).Where(x => ids.Contains(x.ClassSessionId)).ToListAsync(ct);
        if (classes.Count != ids.Distinct().Count() || classes.Count == 0) return "Select valid classes.";
        if (studentId.HasValue)
        {
            var snapshot=await EnrollmentAvailability.LoadAsync(db,ids,studentId,applicationId,ct);
            if(classes.Any(c=>snapshot.SubjectBlocked(studentId.Value,c.SubjectId,start,AcademicYearPolicy.ClassEnd(c))))
                return "This subject overlaps an existing enrollment or pending application. Choose a start date after it ends.";
        }
        if (classes.GroupBy(c => c.SubjectId).Any(g => g.Count() > 1)) return "Select only one class for each subject.";
        if (durationMonths.HasValue) { var planError = AcademicYearPolicy.Validate(start, durationMonths.Value, classes); if (planError != null) return planError; }
        var cancelled = await db.ClassLessonChanges.AsNoTracking().Where(x => ids.Contains(x.ClassSessionId))
            .Select(x => new { x.ClassSessionId, x.OriginalDate }).ToListAsync(ct);
        foreach (var c in classes)
        {
            if (LateMonthEnrollmentPolicy.RemainingDates(start, c.DayOfWeek, ClassScheduleService.FirstDate(c),
                AcademicYearPolicy.ClassEnd(c), c.StartTime, ClassScheduleService.Now,
                cancelled.Where(x => x.ClassSessionId == c.ClassSessionId).Select(x => x.OriginalDate)).Count == 0)
                return $"Class {c.ClassName} has no remaining regular lessons in the requested start month. Choose another start month.";
            if (!c.Subject.IsActive) return $"Subject {c.Subject.SubjectName} is no longer available for enrollment.";
            if (!ClassScheduleService.ValidOn(c, start)) return $"Class {c.ClassName} is unavailable on the selected enrollment start date.";
            if (await OccupiedSeatsAsync(c.ClassSessionId, applicationId, ct, start) >= c.Capacity) return $"Class {c.ClassName} has reached its maximum capacity, including approved applications awaiting payment.";
        }
        var other = studentId.HasValue ? await db.StudentEnrollments.AsNoTracking().Include(x => x.ClassSession)
            .Where(x => x.StudentProfileId == studentId && x.StartDate.Year == start.Year && (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop) &&
                (!x.DropEffectiveDate.HasValue || x.DropEffectiveDate > start) && (!x.EndedAt.HasValue || x.EndedAt > start)).ToListAsync(ct) : new List<StudentEnrollment>();
        other = other.Where(e => AcademicYearPolicy.ClassEnd(e.ClassSession) >= start.Date).ToList();
        foreach (var c in classes)
        {
            // Subject duplication was checked against the actual enrollment
            // intervals above. A later, non-overlapping period is not a duplicate.
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
