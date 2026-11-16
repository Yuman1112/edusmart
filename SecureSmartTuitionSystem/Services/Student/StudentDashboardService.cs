using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.ViewModels.Student;
namespace SecureSmartTuitionSystem.Services.Student;

public sealed class StudentDashboardService(ApplicationDbContext db)
{
    private IQueryable<SecureSmartTuitionSystem.Models.Learning.Assessment> PendingAssessments(ClaimsPrincipal user, int studentId, DateTime now) =>
        new AssessmentService(db).Visible(user).Where(a =>
            a.ResultsPublishedAtUtc == null && a.DueAtMyt > now &&
            a.Participants.Any(p => p.StudentProfileId == studentId && p.SubmittedAtUtc == null));

    public async Task<StudentDashboardViewModel?> BuildAsync(ClaimsPrincipal user)
    {
        if (!user.IsInRole("Student")) return null;
        var now = ClassScheduleService.Now;
        var schedule = await new LearningScheduleReader(db).BuildAsync(user, null, now.Date, "today");
        if (schedule?.Student is not { } student) return null;
        // Use exactly the same class visibility as the learning modules.
        var classes = await new LearningMaterialService(db).Classes(user)
            .Include(x => x.Subject).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToListAsync();
        var pending = PendingAssessments(user, student.StudentProfileId, now);
        var count = await pending.CountAsync();
        var assessments = await pending.OrderBy(a => a.DueAtMyt).ThenBy(a => a.AssessmentId).Take(3)
            .Select(a => new StudentDashboardAssessment { Id = a.AssessmentId, Title = a.Title,
                Subject = a.ClassSession.Subject.SubjectName, DueAtMyt = a.DueAtMyt }).ToListAsync();
        return new StudentDashboardViewModel
        {
            StudentName = student.FullName, StudentUserName = user.Identity?.Name ?? student.StudentNumber,
            AcademicLevel = student.AcademicLevel, School = student.School, Today = now.Date,
            NextStartDate = schedule.NextStartDate, TodayLessons = schedule.Lessons,
            ActiveClassCount = classes.Count, AssessmentsDueCount = count, UpcomingAssessments = assessments,
            Classes = classes.OrderBy(c => ((int)c.DayOfWeek + 6) % 7).ThenBy(c => c.StartTime)
                .Select(c => new StudentDashboardClassViewModel { ClassSessionId = c.ClassSessionId,
                    ClassCode = c.ClassCode, ClassName = c.ClassName ?? "", SubjectName = c.Subject.SubjectName,
                    Day = c.DayOfWeek.ToString(), StartTime = c.StartTime.ToString("hh:mm tt"), EndTime = c.EndTime.ToString("hh:mm tt") }).ToList()
        };
    }
}
