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

        private static readonly string[] OrderedAcademicLevels =
        {
            "YEAR 1",
            "YEAR 2",
            "YEAR 3",
            "YEAR 4",
            "YEAR 5",
            "YEAR 6",
            "FORM 1",
            "FORM 2",
            "FORM 3",
            "FORM 4",
            "FORM 5"
        };

        public ClassesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? subject,
            string? level,
            string? stage)
        {
            subject = NormalizeOptionalValue(subject);
            level = NormalizeOptionalValue(level);
            stage = NormalizeOptionalValue(stage);

            EducationStage? selectedStage = stage?.ToUpperInvariant() switch
            {
                "PRIMARY" => EducationStage.Primary,
                "SECONDARY" => EducationStage.Secondary,
                _ => null
            };

            var query = _context.ClassSessions
                .AsNoTracking()
                .Where(session =>
                    session.Status == ClassSessionStatus.Active &&
                    session.Subject.IsActive &&

                    // Only show classes with remaining seats.
                    _context.StudentEnrollments.Count(enrollment =>
                        enrollment.ClassSessionId ==
                            session.ClassSessionId &&
                        (
                            enrollment.Status ==
                                StudentEnrollmentStatus.Active ||

                            enrollment.Status ==
                                StudentEnrollmentStatus.PendingDrop
                        )) < session.Capacity);

            /*
             * Primary button:
             * Only Year 1 until Year 6.
             *
             * Secondary button:
             * Only Form 1 until Form 5.
             *
             * Normal Classes button:
             * No stage filter, so all valid classes are shown.
             */
            if (selectedStage.HasValue)
            {
                query = query.Where(session =>
                    session.Subject.EducationStage ==
                        selectedStage.Value);
            }

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
                .OrderBy(session =>
                    session.Subject.AcademicLevel == "YEAR 1" ? 1 :
                    session.Subject.AcademicLevel == "YEAR 2" ? 2 :
                    session.Subject.AcademicLevel == "YEAR 3" ? 3 :
                    session.Subject.AcademicLevel == "YEAR 4" ? 4 :
                    session.Subject.AcademicLevel == "YEAR 5" ? 5 :
                    session.Subject.AcademicLevel == "YEAR 6" ? 6 :
                    session.Subject.AcademicLevel == "FORM 1" ? 7 :
                    session.Subject.AcademicLevel == "FORM 2" ? 8 :
                    session.Subject.AcademicLevel == "FORM 3" ? 9 :
                    session.Subject.AcademicLevel == "FORM 4" ? 10 :
                    session.Subject.AcademicLevel == "FORM 5" ? 11 :
                    99)
                .ThenBy(session =>
                    session.Subject.SubjectName)
                .ThenBy(session =>
                    session.DayOfWeek)
                .ThenBy(session =>
                    session.StartTime)
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
                                    (
                                        enrollment.Status ==
                                            StudentEnrollmentStatus.Active ||

                                        enrollment.Status ==
                                            StudentEnrollmentStatus.PendingDrop
                                    ))
                    })
                .ToListAsync();

            /*
             * Only show subjects and academic levels that currently
             * have valid classes with remaining seats.
             */
            var availableSubjects = classes
                .Select(item => item.SubjectName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToList();

            var availableLevelNames = classes
                .Select(item => item.AcademicLevel)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sortedAcademicLevels = OrderedAcademicLevels
                .Where(availableLevelNames.Contains)
                .ToList();

            /*
             * Keep any unexpected academic level at the end instead
             * of removing it completely.
             */
            sortedAcademicLevels.AddRange(
                availableLevelNames
                    .Where(item =>
                        !OrderedAcademicLevels.Contains(
                            item,
                            StringComparer.OrdinalIgnoreCase))
                    .OrderBy(item => item));

            var model = new BrowseClassesViewModel
            {
                SelectedSubject = subject,
                SelectedLevel = level,
                Subjects = availableSubjects,
                AcademicLevels = sortedAcademicLevels,
                Classes = classes
            };

            ViewData["SelectedStage"] =
                selectedStage?.ToString();

            return View(model);
        }

        private static string? NormalizeOptionalValue(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToUpperInvariant();
        }
    }
}