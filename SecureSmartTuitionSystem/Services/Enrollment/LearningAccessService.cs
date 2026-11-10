using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class LearningAccessService(ApplicationDbContext db)
{
    // Catalogue membership for a whole month; this never grants lesson or Meet access.
    public static bool EnrolledInMonth(StudentEnrollment e, DateTime month)
    {
        if (e.Status is not (StudentEnrollmentStatus.Active or StudentEnrollmentStatus.PendingDrop or
            StudentEnrollmentStatus.Dropped or StudentEnrollmentStatus.Completed)) return false;
        var from = new DateTime(month.Year, month.Month, 1);
        var until = from.AddMonths(1);
        var start = e.StartDate.Date > ClassScheduleService.FirstDate(e.ClassSession)
            ? e.StartDate.Date : ClassScheduleService.FirstDate(e.ClassSession);
        var end = new[] { AcademicYearPolicy.EndOfYear(e.StartDate).AddDays(1),
            AcademicYearPolicy.ClassEnd(e.ClassSession).AddDays(1),
            e.DropEffectiveDate?.Date ?? DateTime.MaxValue, e.EndedAt?.Date ?? DateTime.MaxValue }.Min();
        return start < end && start < until && end > from;
    }

    // Display the confirmed application plan, clipped at the actual withdrawal/end date.
    // A missing application is not evidence of a year-long enrollment plan.
    public static string EnrollmentPeriod(StudentEnrollment e)
    {
        var app = e.EnrollmentApplication;
        if (app == null || app.EnrollmentDurationMonths is < 1 or > 12) return "—";
        var start = new DateTime(e.StartDate.Year, e.StartDate.Month, 1);
        var planStart = new DateTime(app.RequestedStartDate.Year, app.RequestedStartDate.Month, 1);
        var end = new[] { planStart.AddMonths(app.EnrollmentDurationMonths),
            AcademicYearPolicy.EndOfYear(e.StartDate).AddDays(1),
            AcademicYearPolicy.ClassEnd(e.ClassSession).AddDays(1),
            e.DropEffectiveDate?.Date ?? DateTime.MaxValue,
            e.EndedAt?.Date ?? DateTime.MaxValue }.Min();
        if (end <= e.StartDate.Date || end <= start) return "—";
        var last = end.AddDays(-1);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (start.Year == last.Year && start.Month == last.Month) return start.ToString("MMM yyyy", culture);
        if (start.Year == last.Year) return start.ToString("MMM", culture) + "–" + last.ToString("MMM yyyy", culture);
        return start.ToString("MMM yyyy", culture) + "–" + last.ToString("MMM yyyy", culture);
    }

    public static bool Started(StudentEnrollment e, DateTime date) => e.StartDate.Date <= date.Date && e.StartDate.Year == date.Year &&
        (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop) &&
        (!e.DropEffectiveDate.HasValue || date.Date < e.DropEffectiveDate.Value.Date) &&
        (!e.EndedAt.HasValue || date.Date < e.EndedAt.Value.Date);
    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, int classId)
    {
        string? id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == null) return false;
        if (user.IsInRole("Admin")) return true;
        if (user.IsInRole("Tutor")) return await db.ClassSessions.AnyAsync(x => x.ClassSessionId == classId && x.TutorUserId == id);
        bool isStudent = user.IsInRole("Student"), isParent = user.IsInRole("Parent");
        var today = ClassScheduleService.Today;
        return await db.StudentEnrollments.AnyAsync(e => e.ClassSessionId == classId && e.StartDate <= today && e.StartDate.Year == today.Year &&
            (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop) &&
            (!e.DropEffectiveDate.HasValue || e.DropEffectiveDate.Value > today) && (!e.EndedAt.HasValue || e.EndedAt.Value > today) &&
            e.ClassSession.Status == ClassSessionStatus.Active && (e.ClassSession.StartDate == null || e.ClassSession.StartDate <= today) &&
            (e.ClassSession.EndDate == null || e.ClassSession.EndDate >= today) &&
            ((isStudent && e.StudentProfile.ApplicationUserId == id) ||
             (isParent && e.StudentProfile.ParentStudents.Any(link => link.IsActive && link.ParentProfile.ApplicationUserId == id))));
    }
}
