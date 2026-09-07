using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    public class ClassesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? subject,
            string? level)
        {
            var query = _context.ClassSessions
                .AsNoTracking()
                .Where(session =>
                    session.Status == ClassSessionStatus.Active &&
                    session.Subject.IsActive);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                query = query.Where(session =>
                    session.Subject.SubjectName == subject);
            }

            if (!string.IsNullOrWhiteSpace(level))
            {
                query = query.Where(session =>
                    session.Subject.AcademicLevel == level);
            }

            var classes = await query
                .OrderBy(session => session.Subject.SubjectName)
                .ThenBy(session => session.DayOfWeek)
                .ThenBy(session => session.StartTime)
                .Select(session =>
                    new AvailableClassItemViewModel
                    {
                        ClassSessionId =
                            session.ClassSessionId,

                        ClassCode =
                            session.ClassCode,

                        SubjectName =
                            session.Subject.SubjectName,

                        AcademicLevel =
                            session.Subject.AcademicLevel,

                        TutorName =
                            session.TutorUser.FullName
                            ?? session.TutorUser.Email
                            ?? "Tutor",

                        Day =
                            session.DayOfWeek.ToString(),

                        StartTime =
                            session.StartTime.ToString("hh:mm tt"),

                        EndTime =
                            session.EndTime.ToString("hh:mm tt"),

                        Venue =
                            session.Venue,

                        MonthlyFee =
                            session.Subject.MonthlyFee,

                        Capacity =
                            session.Capacity,

                        SeatsRemaining =
                            session.Capacity -
                            _context.StudentEnrollments.Count(
                                enrollment =>
                                    enrollment.ClassSessionId ==
                                        session.ClassSessionId &&
                                    (enrollment.Status ==
                                        StudentEnrollmentStatus.Active ||
                                     enrollment.Status ==
                                        StudentEnrollmentStatus.PendingDrop))
                    })
                .ToListAsync();

            var model = new BrowseClassesViewModel
            {
                SelectedSubject = subject,
                SelectedLevel = level,
                Classes = classes,

                Subjects = await _context.Subjects
                    .AsNoTracking()
                    .Where(item => item.IsActive)
                    .Select(item => item.SubjectName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync(),

                AcademicLevels = await _context.Subjects
                    .AsNoTracking()
                    .Where(item => item.IsActive)
                    .Select(item => item.AcademicLevel)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync()
            };

            return View(model);
        }
    }
}