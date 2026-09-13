using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Admin;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var tutors =
                await _userManager.GetUsersInRoleAsync("Tutor");

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

                RecentClasses =
                    await _context.ClassSessions
                        .AsNoTracking()
                        .Include(session => session.Subject)
                        .OrderByDescending(session =>
                            session.CreatedAt)
                        .Take(5)
                        .Select(session =>
                            new AdminClassItemViewModel
                            {
                                ClassCode =
                                    session.ClassCode,

                                SubjectName =
                                    session.Subject.SubjectName,

                                AcademicLevel =
                                    session.Subject.AcademicLevel,

                                Schedule =
                                    session.DayOfWeek + ", " +
                                    session.StartTime
                                        .ToString("hh:mm tt")
                            })
                        .ToListAsync(),

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
