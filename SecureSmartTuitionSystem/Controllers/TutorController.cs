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
         * =====================================================
         * TUTOR DASHBOARD
         * =====================================================
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
                        session.DayOfWeek ==
                            DayOfWeek.Monday ? 1 :
                        session.DayOfWeek ==
                            DayOfWeek.Tuesday ? 2 :
                        session.DayOfWeek ==
                            DayOfWeek.Wednesday ? 3 :
                        session.DayOfWeek ==
                            DayOfWeek.Thursday ? 4 :
                        session.DayOfWeek ==
                            DayOfWeek.Friday ? 5 :
                        session.DayOfWeek ==
                            DayOfWeek.Saturday ? 6 :
                        session.DayOfWeek ==
                            DayOfWeek.Sunday ? 7 : 8)
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

            var model =
                new TutorDashboardViewModel
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
         * =====================================================
         * TUTOR MY CLASSES
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> MyClasses(
            string? subjectName,
            DayOfWeek? dayOfWeek,
            ClassSessionStatus? status)
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            subjectName =
                string.IsNullOrWhiteSpace(subjectName)
                    ? null
                    : subjectName.Trim();

            /*
             * This query always limits the records to the
             * currently logged-in Tutor.
             */
            var tutorClassesQuery =
                _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Where(session =>
                        session.TutorUserId == tutor.Id);

            /*
             * Summary values use all classes assigned
             * to the Tutor, before filters are applied.
             */
            int totalClassCount =
                await tutorClassesQuery.CountAsync();

            int activeClassCount =
                await tutorClassesQuery.CountAsync(session =>
                    session.Status ==
                        ClassSessionStatus.Active);

            DayOfWeek today =
                DateTime.Today.DayOfWeek;

            int todayClassCount =
                await tutorClassesQuery.CountAsync(session =>
                    session.Status ==
                        ClassSessionStatus.Active &&
                    session.DayOfWeek == today);

            /*
             * Subject filter options only include subjects
             * belonging to this Tutor's assigned classes.
             */
            var subjectOptions =
                await tutorClassesQuery
                    .Select(session =>
                        session.Subject.SubjectName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync();

            /*
             * Apply filters.
             */
            if (subjectName != null)
            {
                tutorClassesQuery =
                    tutorClassesQuery.Where(session =>
                        session.Subject.SubjectName ==
                            subjectName);
            }

            if (dayOfWeek.HasValue)
            {
                tutorClassesQuery =
                    tutorClassesQuery.Where(session =>
                        session.DayOfWeek ==
                            dayOfWeek.Value);
            }

            if (status.HasValue)
            {
                tutorClassesQuery =
                    tutorClassesQuery.Where(session =>
                        session.Status ==
                            status.Value);
            }

            /*
             * Monday to Sunday schedule order.
             */
            var assignedClasses =
                await tutorClassesQuery
                    .OrderBy(session =>
                        session.Status)
                    .ThenBy(session =>
                        session.DayOfWeek ==
                            DayOfWeek.Monday ? 1 :
                        session.DayOfWeek ==
                            DayOfWeek.Tuesday ? 2 :
                        session.DayOfWeek ==
                            DayOfWeek.Wednesday ? 3 :
                        session.DayOfWeek ==
                            DayOfWeek.Thursday ? 4 :
                        session.DayOfWeek ==
                            DayOfWeek.Friday ? 5 :
                        session.DayOfWeek ==
                            DayOfWeek.Saturday ? 6 :
                        session.DayOfWeek ==
                            DayOfWeek.Sunday ? 7 : 8)
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

            var model =
                new TutorMyClassesViewModel
                {
                    TutorName =
                        tutor.FullName ??
                        tutor.Email ??
                        "Tutor",

                    TotalClassCount =
                        totalClassCount,

                    ActiveClassCount =
                        activeClassCount,

                    TodayClassCount =
                        todayClassCount,

                    SubjectName =
                        subjectName,

                    DayOfWeek =
                        dayOfWeek,

                    Status =
                        status,

                    SubjectOptions =
                        subjectOptions,

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
                                        session.Subject
                                            .AcademicLevel +
                                        " - " +
                                        session.Subject
                                            .SubjectName,

                                    EducationStage =
                                        session.Subject
                                            .EducationStage
                                            .ToString(),

                                    AcademicLevel =
                                        session.Subject
                                            .AcademicLevel,

                                    SubjectName =
                                        session.Subject
                                            .SubjectName,

                                    Day =
                                        session.DayOfWeek
                                            .ToString(),

                                    StartTime =
                                        session.StartTime
                                            .ToString("hh:mm tt"),

                                    EndTime =
                                        session.EndTime
                                            .ToString("hh:mm tt"),

                                    GoogleMeetLink =
                                        session.Venue,

                                    Capacity =
                                        session.Capacity,

                                    StudentCount =
                                        studentCounts
                                            .TryGetValue(
                                                session
                                                    .ClassSessionId,
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


        /*
         * =====================================================
         * TUTOR CLASS DETAILS
         * =====================================================
         */

        [HttpGet]
        public async Task<IActionResult> ClassDetails(
            int id)
        {
            var tutor =
                await _userManager.GetUserAsync(User);

            if (tutor == null)
            {
                return Challenge();
            }

            /*
             * TutorUserId is included in the query.
             * Therefore, a Tutor cannot view another
             * Tutor's class by changing the URL ID.
             */
            var session =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(item => item.Subject)
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id &&
                        item.TutorUserId == tutor.Id);

            if (session == null)
            {
                return NotFound();
            }

            int studentCount =
                await _context.StudentEnrollments
                    .AsNoTracking()
                    .CountAsync(enrollment =>
                        enrollment.ClassSessionId ==
                            session.ClassSessionId &&
                        enrollment.Status ==
                            StudentEnrollmentStatus.Active);

            var model =
                new TutorClassDetailsViewModel
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

                    Capacity =
                        session.Capacity,

                    StudentCount =
                        studentCount,

                    Remark =
                        session.Remark,

                    Status =
                        session.Status
                };

            return View(model);
        }
    }
}