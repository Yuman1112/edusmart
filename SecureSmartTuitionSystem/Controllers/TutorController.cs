using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Tutor")]
    public class TutorController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;

        public TutorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /*
         * Tutor Dashboard
         */
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            var activeClasses =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        session.TutorUserId == tutor.Id &&
                        session.Status ==
                            ClassSessionStatus.Active)
                    .OrderBy(session =>
                        session.DayOfWeek)
                    .ThenBy(session =>
                        session.StartTime)
                    .ToListAsync();

            var classIds =
                activeClasses
                    .Select(session =>
                        session.ClassSessionId)
                    .ToList();

            var studentCounts =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment =>
                        classIds.Contains(
                            enrollment.ClassSessionId) &&
                        enrollment.Status ==
                            StudentEnrollmentStatus.Active)
                    .GroupBy(enrollment =>
                        enrollment.ClassSessionId)
                    .Select(group =>
                        new
                        {
                            ClassSessionId = group.Key,
                            StudentCount = group.Count()
                        })
                    .ToDictionaryAsync(
                        item => item.ClassSessionId,
                        item => item.StudentCount);

            int activeStudentCount =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment =>
                        classIds.Contains(
                            enrollment.ClassSessionId) &&
                        enrollment.Status ==
                            StudentEnrollmentStatus.Active)
                    .Select(enrollment =>
                        enrollment.StudentProfileId)
                    .Distinct()
                    .CountAsync();

            var model = new TutorDashboardViewModel
            {
                TutorName =
                    tutor.FullName ??
                    tutor.Email ??
                    "Tutor",

                AssignedClassCount =
                    activeClasses.Count,

                TodayClassCount =
                    activeClasses.Count(session =>
                        session.DayOfWeek ==
                            DateTime.Today.DayOfWeek),

                ActiveStudentCount =
                    activeStudentCount,

                Classes =
                    activeClasses
                        .Select(session =>
                            new TutorClassItemViewModel
                            {
                                ClassSessionId =
                                    session.ClassSessionId,

                                ClassCode =
                                    session.ClassCode,

                                SubjectName =
                                    session.Subject.SubjectName,

                                AcademicLevel =
                                    session.Subject.AcademicLevel,

                                Day =
                                    session.DayOfWeek.ToString(),

                                Time =
                                    session.StartTime
                                        .ToString("hh:mm tt") +
                                    " - " +
                                    session.EndTime
                                        .ToString("hh:mm tt"),

                                Venue =
                                    session.Venue,

                                StudentCount =
                                    studentCounts.TryGetValue(
                                        session.ClassSessionId,
                                        out int count)
                                            ? count
                                            : 0
                            })
                        .ToList()
            };

            return View(model);
        }

        /*
         * Display every class assigned to the logged-in Tutor.
         */
        [HttpGet]
        public async Task<IActionResult> MyClasses()
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            var assignedClasses =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        session.TutorUserId == tutor.Id)
                    .OrderBy(session =>
                        session.Status)
                    .ThenBy(session =>
                        session.DayOfWeek)
                    .ThenBy(session =>
                        session.StartTime)
                    .ToListAsync();

            var classIds =
                assignedClasses
                    .Select(session =>
                        session.ClassSessionId)
                    .ToList();

            var studentCounts =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment =>
                        classIds.Contains(
                            enrollment.ClassSessionId) &&
                        enrollment.Status ==
                            StudentEnrollmentStatus.Active)
                    .GroupBy(enrollment =>
                        enrollment.ClassSessionId)
                    .Select(group =>
                        new
                        {
                            ClassSessionId = group.Key,
                            StudentCount = group.Count()
                        })
                    .ToDictionaryAsync(
                        item => item.ClassSessionId,
                        item => item.StudentCount);

            var model = new TutorMyClassesViewModel
            {
                TutorName =
                    tutor.FullName ??
                    tutor.Email ??
                    "Tutor",

                TotalClassCount =
                    assignedClasses.Count,

                ActiveClassCount =
                    assignedClasses.Count(session =>
                        session.Status ==
                            ClassSessionStatus.Active),

                TodayClassCount =
                    assignedClasses.Count(session =>
                        session.Status ==
                            ClassSessionStatus.Active &&
                        session.DayOfWeek ==
                            DateTime.Today.DayOfWeek),

                Classes =
                    assignedClasses
                        .Select(session =>
                            new TutorMyClassItemViewModel
                            {
                                ClassSessionId =
                                    session.ClassSessionId,

                                ClassCode =
                                    session.ClassCode,

                                ClassName =
                                    session.ClassName ??
                                    session.Subject.AcademicLevel +
                                    " - " +
                                    session.Subject.SubjectName,

                                EducationStage =
                                    session.Subject
                                        .EducationStage
                                        .ToString(),

                                AcademicLevel =
                                    session.Subject.AcademicLevel,

                                SubjectName =
                                    session.Subject.SubjectName,

                                Day =
                                    session.DayOfWeek.ToString(),

                                StartTime =
                                    session.StartTime
                                        .ToString("hh:mm tt"),

                                EndTime =
                                    session.EndTime
                                        .ToString("hh:mm tt"),

                                GoogleMeetLink =
                                    session.Venue,

                                MonthlyFee =
                                    session.Subject.MonthlyFee,

                                Capacity =
                                    session.Capacity,

                                StudentCount =
                                    studentCounts.TryGetValue(
                                        session.ClassSessionId,
                                        out int count)
                                            ? count
                                            : 0,

                                Status =
                                    session.Status
                            })
                        .ToList()
            };

            return View(model);
        }
    }
}