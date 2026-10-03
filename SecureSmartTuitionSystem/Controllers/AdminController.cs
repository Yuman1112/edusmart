using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Admin;
using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ClassScheduleService _schedules;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ClassScheduleService schedules)
        {
            _context = context;
            _userManager = userManager;
            _schedules = schedules;
        }

        public async Task<IActionResult> Index()
        {
            var tutors =
                await _userManager.GetUsersInRoleAsync("Tutor");

            var today = ClassScheduleService.Today;
            var lessons = await _schedules.GetLessonsAsync(today, today);

            var model = new AdminDashboardViewModel
            {
                StudentCount = await _context.StudentProfiles
                    .CountAsync(student => student.IsActive),

                TutorCount = tutors.Count,

                ActiveClassCount = await _context.ClassSessions
                    .CountAsync(session =>
                        session.Status ==
                            ClassSessionStatus.Active),

                PendingEnrollmentCount =
                    await _context.EnrollmentApplications
                        .CountAsync(application =>
                            application.Status ==
                                EnrollmentStatus.PendingReview),

                TodayClasses = lessons.Select(lesson => new AdminClassItemViewModel
                {
                    ClassName = lesson.Class.ClassName ?? "Class",
                    Schedule = $"{lesson.Start:hh:mm tt} – {lesson.End:hh:mm tt} MYT",
                    Status = lesson.Status,
                    StatusCssClass = lesson.Cancelled ? "status-danger"
                        : lesson.Replacement ? "status-warning" : "status-success"
                }).ToList(),

                PendingEnrollments =
                    await _context.EnrollmentApplications
                        .AsNoTracking()
                        .Where(application =>
                            application.Status ==
                                EnrollmentStatus.PendingReview)
                        .OrderByDescending(application =>
                            application.SubmittedAt)
                        .Take(5)
                        .Select(application =>
                            new PendingEnrollmentItemViewModel
                            {
                                EnrollmentApplicationId =
                                    application
                                        .EnrollmentApplicationId,

                                ApplicationNumber =
                                    application.ApplicationNumber,

                                StudentName =
                                    application.StudentName,

                                GuardianName =
                                    application.GuardianName,

                                Status =
                                    application.Status.ToString(),

                                SubmittedAt =
                                    application.SubmittedAt
                            })
                        .ToListAsync()
            };

            return View(model);
        }
    }
}
