using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;

// Membership is [Start, End): an effective withdrawal frees the seat on that date.
public sealed record EnrollmentWindow(int ClassId, int SubjectId, int? StudentId, int? ApplicationId,
    DateTime Start, DateTime End, bool ReservesSeat);
public sealed class EnrollmentAvailability(List<EnrollmentWindow> windows)
{
    public static bool Overlaps(EnrollmentWindow w, DateTime start, DateTime end) => w.Start < end && start < w.End;
    public bool SubjectBlocked(int studentId, int subjectId, DateTime start, DateTime end) =>
        windows.Any(w => w.StudentId == studentId && w.SubjectId == subjectId && Overlaps(w, start.Date, end.Date.AddDays(1)));
    public int PeakSeats(int classId, DateTime start, DateTime end)
    {
        if (end.Date < start.Date) return 0;
        var until = end.Date.AddDays(1);
        var events = windows.Where(w => w.ClassId == classId && w.ReservesSeat && Overlaps(w,start.Date,until))
            .SelectMany(w => new[] { (Date: w.Start > start.Date ? w.Start : start.Date, Change:1),
                (Date: w.End < until ? w.End : until, Change:-1) });
        int active=0, peak=0;
        foreach(var day in events.GroupBy(x=>x.Date).OrderBy(x=>x.Key)) { active+=day.Sum(x=>x.Change); peak=Math.Max(peak,active); }
        return peak;
    }
    public static async Task<EnrollmentAvailability> LoadAsync(ApplicationDbContext db, List<int> classIds, int? studentId=null, int? excludeApplicationId=null, CancellationToken ct=default)
    {
        var records=await db.StudentEnrollments.AsNoTracking().Include(e=>e.ClassSession)
            .Where(e=>classIds.Contains(e.ClassSessionId) || (studentId.HasValue && e.StudentProfileId==studentId))
            .Where(e=>!excludeApplicationId.HasValue || e.EnrollmentApplicationId!=excludeApplicationId).ToListAsync(ct);
        var windows=new List<EnrollmentWindow>();
        foreach(var e in records) {
            if(e.Status==StudentEnrollmentStatus.Dropped && !e.DropEffectiveDate.HasValue && !e.EndedAt.HasValue)continue;
            if(e.Status==StudentEnrollmentStatus.Completed && !e.EndedAt.HasValue)continue;
            var end=new[] { AcademicYearPolicy.ClassEnd(e.ClassSession).AddDays(1),AcademicYearPolicy.EndOfYear(e.StartDate).AddDays(1),e.DropEffectiveDate??DateTime.MaxValue,e.EndedAt??DateTime.MaxValue }.Min().Date;
            var start=e.StartDate.Date>ClassScheduleService.FirstDate(e.ClassSession)?e.StartDate.Date:ClassScheduleService.FirstDate(e.ClassSession);
            if(start<end) windows.Add(new(e.ClassSessionId,e.ClassSession.SubjectId,e.StudentProfileId,e.EnrollmentApplicationId,start,end,true));
        }
        var pending=await db.Set<EnrollmentApplicationSubject>().AsNoTracking().Include(x=>x.ClassSession).Include(x=>x.EnrollmentApplication)
            .Where(x=>classIds.Contains(x.ClassSessionId)||(studentId.HasValue&&x.EnrollmentApplication.ExistingStudentProfileId==studentId))
            .Where(x=>!excludeApplicationId.HasValue||x.EnrollmentApplicationId!=excludeApplicationId)
            .Where(x=>x.EnrollmentApplication.Status==EnrollmentStatus.PendingReview||x.EnrollmentApplication.Status==EnrollmentStatus.ApprovedPendingPayment||x.EnrollmentApplication.Status==EnrollmentStatus.PaymentConfirmed).ToListAsync(ct);
        foreach(var p in pending) {
            if(records.Any(e=>e.EnrollmentApplicationId==p.EnrollmentApplicationId&&e.ClassSessionId==p.ClassSessionId))continue;
            var app=p.EnrollmentApplication;var end=AcademicYearPolicy.ClassEnd(p.ClassSession).AddDays(1);
            var yearEnd=AcademicYearPolicy.EndOfYear(app.RequestedStartDate).AddDays(1);if(yearEnd<end)end=yearEnd;
            var start=app.RequestedStartDate.Date>ClassScheduleService.FirstDate(p.ClassSession)?app.RequestedStartDate.Date:ClassScheduleService.FirstDate(p.ClassSession);
            if(start<end)windows.Add(new(p.ClassSessionId,p.ClassSession.SubjectId,app.ExistingStudentProfileId,app.EnrollmentApplicationId,start,end,app.Status!=EnrollmentStatus.PendingReview));
        }
        return new(windows);
    }
}
