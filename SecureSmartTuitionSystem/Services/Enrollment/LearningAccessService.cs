using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public sealed class LearningAccessService(ApplicationDbContext db)
{
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
