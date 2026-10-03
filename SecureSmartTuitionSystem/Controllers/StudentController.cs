using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Student;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;

        public StudentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string? currentUserId =
                _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                return Challenge();
            }

            var studentProfile =
                await _context.StudentProfiles
                    .AsNoTracking()
                    .Include(student =>
                        student.ApplicationUser)
                    .FirstOrDefaultAsync(student =>
                        student.ApplicationUserId ==
                            currentUserId);

            if (studentProfile == null)
            {
                return NotFound();
            }

            var classes =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Include(enrollment =>
                        enrollment.ClassSession)
                        .ThenInclude(classSession =>
                            classSession.Subject)
                    .Where(enrollment =>
                        enrollment.StudentProfileId ==
                            studentProfile.StudentProfileId &&
                        enrollment.StartDate <= DateTime.UtcNow.AddHours(8).Date &&
                        enrollment.StartDate.Year == DateTime.UtcNow.AddHours(8).Year &&
                        (!enrollment.EndedAt.HasValue || enrollment.EndedAt > DateTime.UtcNow.AddHours(8).Date) &&
                        (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate > DateTime.UtcNow.AddHours(8).Date) &&
                        (enrollment.ClassSession.StartDate ?? enrollment.ClassSession.CreatedAt.AddHours(8)).Year == DateTime.UtcNow.AddHours(8).Year &&
                        enrollment.ClassSession.Status == ClassSessionStatus.Active &&
                        (enrollment.ClassSession.StartDate == null || enrollment.ClassSession.StartDate <= DateTime.UtcNow.AddHours(8).Date) &&
                        (enrollment.ClassSession.EndDate == null || enrollment.ClassSession.EndDate >= DateTime.UtcNow.AddHours(8).Date) &&
                        (enrollment.Status == StudentEnrollmentStatus.Active || (enrollment.Status == StudentEnrollmentStatus.PendingDrop && (!enrollment.DropEffectiveDate.HasValue || enrollment.DropEffectiveDate.Value > DateTime.UtcNow.AddHours(8).Date))))
                    .OrderBy(enrollment =>
                        enrollment.ClassSession.DayOfWeek)
                    .ThenBy(enrollment =>
                        enrollment.ClassSession.StartTime)
                    .Select(enrollment =>
                        new StudentDashboardClassViewModel
                        {
                            ClassCode =
                                enrollment.ClassSession
                                    .ClassCode,

                            ClassName =
                                enrollment.ClassSession
                                    .ClassName
                                ?? string.Empty,

                            SubjectName =
                                enrollment.ClassSession
                                    .Subject.SubjectName,

                            Day =
                                enrollment.ClassSession
                                    .DayOfWeek
                                    .ToString(),

                            StartTime =
                                enrollment.ClassSession
                                    .StartTime
                                    .ToString("hh:mm tt"),

                            EndTime =
                                enrollment.ClassSession
                                    .EndTime
                                    .ToString("hh:mm tt")
                        })
                    .ToListAsync();

            var model =
                new StudentDashboardViewModel
                {
                    StudentName =
                        studentProfile.FullName,

                    StudentUserName =
                        studentProfile.ApplicationUser?
                            .UserName
                        ?? studentProfile.StudentNumber,

                    School =
                        studentProfile.School,

                    AcademicLevel =
                        studentProfile.AcademicLevel,

                    ActiveClassCount =
                        classes.Count,

                    Classes =
                        classes
                };

            return View(model);
        }
    }
}