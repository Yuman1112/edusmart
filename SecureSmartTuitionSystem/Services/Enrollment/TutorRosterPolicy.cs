using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public static class TutorRosterPolicy
{
    // Read-only roster membership, not permission to join a lesson or access materials.
    public static IQueryable<StudentEnrollment> Query(ApplicationDbContext db, string tutorId, DateTime today) =>
        Filter(db.StudentEnrollments.AsNoTracking(), tutorId, today);
    public static IQueryable<StudentEnrollment> Filter(IQueryable<StudentEnrollment> enrollments, string tutorId, DateTime today) =>
        enrollments.Where(e =>
            e.ClassSession.TutorUserId == tutorId && e.ClassSession.Status == ClassSessionStatus.Active &&
            e.StudentProfile.IsActive &&
            (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop) &&
            (e.ClassSession.StartDate ?? e.ClassSession.CreatedAt.AddHours(8)).Year >= today.Year &&
            e.StartDate.Year <= (e.ClassSession.StartDate ?? e.ClassSession.CreatedAt.AddHours(8)).Year &&
            (e.ClassSession.EndDate == null || (e.ClassSession.EndDate.Value.Date >= today && e.ClassSession.EndDate.Value.Date >= e.StartDate.Date)) &&
            (e.DropEffectiveDate == null || (e.DropEffectiveDate > today && e.DropEffectiveDate > e.StartDate && e.DropEffectiveDate > (e.ClassSession.StartDate ?? e.ClassSession.CreatedAt.AddHours(8)).Date)) &&
            (e.EndedAt == null || (e.EndedAt > today && e.EndedAt > e.StartDate && e.EndedAt > (e.ClassSession.StartDate ?? e.ClassSession.CreatedAt.AddHours(8)).Date)));
}

