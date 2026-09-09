using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ClassSessionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] PrimaryLevels =
        {
            "YEAR 1",
            "YEAR 2",
            "YEAR 3",
            "YEAR 4",
            "YEAR 5",
            "YEAR 6"
        };

        private static readonly string[] SecondaryLevels =
        {
            "FORM 1",
            "FORM 2",
            "FORM 3",
            "FORM 4",
            "FORM 5"
        };

        private static readonly string[] YearOneToThreeSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI"
        };

        private static readonly string[] YearFourToSixSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI",
            "SEJ"
        };

        private static readonly string[] FormOneToThreeSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "SCI",
            "SEJ",
            "GEO",
            "RBT",
            "ASK"
        };

        private static readonly string[] FormFourToFiveSubjects =
        {
            "BC",
            "BMP",
            "BMK",
            "BIP",
            "BIK",
            "MM",
            "AM",
            "BIO",
            "CHE",
            "PHY",
            "SEJ",
            "ACC",
            "EKO",
            "PER"
        };

        public ClassSessionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var sessions =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(session => session.Subject)
                    .Include(session => session.TutorUser)
                    .OrderBy(session =>
                        session.Subject.EducationStage)
                    .ThenBy(session =>
                        session.Subject.AcademicLevel)
                    .ThenBy(session =>
                        session.Subject.SubjectName)
                    .ThenBy(session =>
                        session.DayOfWeek)
                    .ThenBy(session =>
                        session.StartTime)
                    .ToListAsync();

            return View(sessions);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ClassSessionFormViewModel
            {
                Capacity = 20,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(20, 0)
            };

            await PopulateSelectionsAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ClassSessionFormViewModel model)
        {
            NormalizeInput(model);
            await GenerateClassNameIfEmptyAsync(model);

            await ValidateClassSessionAsync(
                model,
                existingClassSessionId: null);

            if (!ModelState.IsValid)
            {
                await PopulateSelectionsAsync(model);
                return View(model);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                Subject subject =
                    await FindOrCreateSubjectAsync(model);

                string classCode =
                    await GenerateNextClassCodeAsync();

                var classSession = new ClassSession
                {
                    ClassCode = classCode,
                    ClassName = model.ClassName!,
                    Subject = subject,
                    TutorUserId = model.TutorUserId,
                    DayOfWeek = model.DayOfWeek!.Value,
                    StartTime = model.StartTime!.Value,
                    EndTime = model.EndTime!.Value,
                    Venue = model.Venue,
                    Capacity = model.Capacity,
                    Status = ClassSessionStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ClassSessions.Add(classSession);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Class {classCode} was created successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(model.ClassName),
                    "The Class Name already exists. " +
                    "Please enter another Class Name.");

                await PopulateSelectionsAsync(model);

                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var session =
                await _context.ClassSessions
                    .AsNoTracking()
                    .Include(item => item.Subject)
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            var model = new ClassSessionFormViewModel
            {
                ClassSessionId = session.ClassSessionId,
                EducationStage = session.Subject.EducationStage,
                AcademicLevel = session.Subject.AcademicLevel,
                SubjectName = session.Subject.SubjectName,
                MonthlyFee = session.Subject.MonthlyFee,
                ClassName = session.ClassName,
                TutorUserId = session.TutorUserId,
                DayOfWeek = session.DayOfWeek,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Venue = session.Venue,
                Capacity = session.Capacity
            };

            await PopulateSelectionsAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ClassSessionFormViewModel model)
        {
            if (model.ClassSessionId != id)
            {
                return BadRequest();
            }

            NormalizeInput(model);
            await GenerateClassNameIfEmptyAsync(model);

            await ValidateClassSessionAsync(
                model,
                existingClassSessionId: id);

            if (!ModelState.IsValid)
            {
                await PopulateSelectionsAsync(model);
                return View(model);
            }

            var session =
                await _context.ClassSessions
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                Subject subject =
                    await FindOrCreateSubjectAsync(model);

                session.Subject = subject;
                session.ClassName = model.ClassName!;
                session.TutorUserId = model.TutorUserId;
                session.DayOfWeek = model.DayOfWeek!.Value;
                session.StartTime = model.StartTime!.Value;
                session.EndTime = model.EndTime!.Value;
                session.Venue = model.Venue;
                session.Capacity = model.Capacity;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Class {session.ClassCode} " +
                    "was updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(model.ClassName),
                    "The Class Name already exists. " +
                    "Please enter another Class Name.");

                await PopulateSelectionsAsync(model);

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var session =
                await _context.ClassSessions
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            session.Status = ClassSessionStatus.Cancelled;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Class {session.ClassCode} was cancelled.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var session =
                await _context.ClassSessions
                    .FirstOrDefaultAsync(item =>
                        item.ClassSessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            session.Status = ClassSessionStatus.Active;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Class {session.ClassCode} was activated.";

            return RedirectToAction(nameof(Index));
        }

        private static void NormalizeInput(
            ClassSessionFormViewModel model)
        {
            model.AcademicLevel =
                NormalizeText(model.AcademicLevel)
                    .ToUpperInvariant();

            model.SubjectName =
                NormalizeText(model.SubjectName)
                    .ToUpperInvariant();

            model.TutorUserId =
                NormalizeText(model.TutorUserId);

            model.Venue =
                NormalizeText(model.Venue);

            model.ClassName =
                string.IsNullOrWhiteSpace(model.ClassName)
                    ? null
                    : NormalizeText(model.ClassName);
        }

        private static string NormalizeText(string? value)
        {
            return Regex.Replace(
                value?.Trim() ?? string.Empty,
                @"\s+",
                " ");
        }

        private async Task GenerateClassNameIfEmptyAsync(
            ClassSessionFormViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.ClassName) ||
                string.IsNullOrWhiteSpace(model.AcademicLevel) ||
                string.IsNullOrWhiteSpace(model.SubjectName) ||
                string.IsNullOrWhiteSpace(model.TutorUserId) ||
                !model.StartTime.HasValue)
            {
                return;
            }

            var tutor =
                await _userManager.FindByIdAsync(
                    model.TutorUserId);

            if (tutor == null)
            {
                return;
            }

            string tutorName =
                tutor.FullName ??
                tutor.Email ??
                "Tutor";

            string startTime =
                DateTime.Today
                    .Add(model.StartTime.Value.ToTimeSpan())
                    .ToString(
                        "hh:mm tt",
                        CultureInfo.InvariantCulture);

            model.ClassName =
                $"{model.AcademicLevel} - " +
                $"{model.SubjectName} - " +
                $"{tutorName} - " +
                $"{startTime}";
        }

        private async Task ValidateClassSessionAsync(
            ClassSessionFormViewModel model,
            int? existingClassSessionId)
        {
            ValidateStageAndLevel(model);
            ValidateSubject(model);
            ValidateTime(model);

            if (!string.IsNullOrWhiteSpace(
                    model.TutorUserId))
            {
                var tutor =
                    await _userManager.FindByIdAsync(
                        model.TutorUserId);

                bool validTutor =
                    tutor != null &&
                    tutor.IsActive &&
                    await _userManager.IsInRoleAsync(
                        tutor,
                        "Tutor");

                if (!validTutor)
                {
                    ModelState.AddModelError(
                        nameof(model.TutorUserId),
                        "The selected Tutor is not valid.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.ClassName))
            {
                ModelState.AddModelError(
                    nameof(model.ClassName),
                    "Class Name is required.");
            }
            else
            {
                bool duplicateName =
                    await _context.ClassSessions.AnyAsync(
                        session =>
                            session.ClassName ==
                                model.ClassName &&
                            (!existingClassSessionId.HasValue ||
                             session.ClassSessionId !=
                                existingClassSessionId.Value));

                if (duplicateName)
                {
                    ModelState.AddModelError(
                        nameof(model.ClassName),
                        "This Class Name already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    model.TutorUserId) &&
                model.DayOfWeek.HasValue &&
                model.StartTime.HasValue &&
                model.EndTime.HasValue &&
                model.StartTime.Value <
                    model.EndTime.Value)
            {
                bool conflict =
                    await _context.ClassSessions.AnyAsync(
                        session =>
                            session.TutorUserId ==
                                model.TutorUserId &&
                            session.DayOfWeek ==
                                model.DayOfWeek.Value &&
                            session.Status ==
                                ClassSessionStatus.Active &&
                            (!existingClassSessionId.HasValue ||
                             session.ClassSessionId !=
                                existingClassSessionId.Value) &&
                            model.StartTime.Value <
                                session.EndTime &&
                            model.EndTime.Value >
                                session.StartTime);

                if (conflict)
                {
                    ModelState.AddModelError(
                        nameof(model.StartTime),
                        "The Tutor already has another class " +
                        "during this time.");
                }
            }
        }

        private void ValidateStageAndLevel(
            ClassSessionFormViewModel model)
        {
            if (!model.EducationStage.HasValue ||
                string.IsNullOrWhiteSpace(
                    model.AcademicLevel))
            {
                return;
            }

            bool validLevel =
                model.EducationStage ==
                    Models.Enrollment.EducationStage.Primary
                    ? PrimaryLevels.Contains(
                        model.AcademicLevel)
                    : SecondaryLevels.Contains(
                        model.AcademicLevel);

            if (!validLevel)
            {
                ModelState.AddModelError(
                    nameof(model.AcademicLevel),
                    "The Academic Level does not match " +
                    "the selected Education Stage.");
            }
        }

        private void ValidateSubject(
            ClassSessionFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(
                    model.AcademicLevel) ||
                string.IsNullOrWhiteSpace(
                    model.SubjectName))
            {
                return;
            }

            string[] allowedSubjects =
                GetSubjectsForLevel(
                    model.AcademicLevel);

            if (!allowedSubjects.Contains(
                    model.SubjectName))
            {
                ModelState.AddModelError(
                    nameof(model.SubjectName),
                    "The selected Subject is not available " +
                    "for this Academic Level.");
            }
        }

        private void ValidateTime(
            ClassSessionFormViewModel model)
        {
            if (!model.StartTime.HasValue ||
                !model.EndTime.HasValue)
            {
                return;
            }

            if (model.StartTime.Value >=
                model.EndTime.Value)
            {
                ModelState.AddModelError(
                    nameof(model.StartTime),
                    "Start Time must be earlier than End Time.");

                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "End Time must be later than Start Time.");

                return;
            }

            TimeSpan duration =
                model.EndTime.Value -
                model.StartTime.Value;

            if (duration < TimeSpan.FromMinutes(30))
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Class duration must be at least 30 minutes.");
            }

            if (duration > TimeSpan.FromHours(4))
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Class duration cannot exceed 4 hours.");
            }
        }

        private static string[] GetSubjectsForLevel(
            string academicLevel)
        {
            return academicLevel switch
            {
                "YEAR 1" or "YEAR 2" or "YEAR 3"
                    => YearOneToThreeSubjects,

                "YEAR 4" or "YEAR 5" or "YEAR 6"
                    => YearFourToSixSubjects,

                "FORM 1" or "FORM 2" or "FORM 3"
                    => FormOneToThreeSubjects,

                "FORM 4" or "FORM 5"
                    => FormFourToFiveSubjects,

                _ => Array.Empty<string>()
            };
        }

        private async Task<Subject> FindOrCreateSubjectAsync(
            ClassSessionFormViewModel model)
        {
            var subject =
                await _context.Subjects
                    .FirstOrDefaultAsync(item =>
                        item.EducationStage ==
                            model.EducationStage!.Value &&
                        item.AcademicLevel ==
                            model.AcademicLevel &&
                        item.SubjectName ==
                            model.SubjectName);

            if (subject != null)
            {
                subject.MonthlyFee =
                    model.MonthlyFee;

                subject.IsActive = true;

                return subject;
            }

            subject = new Subject
            {
                EducationStage =
                    model.EducationStage!.Value,

                AcademicLevel =
                    model.AcademicLevel,

                SubjectName =
                    model.SubjectName,

                MonthlyFee =
                    model.MonthlyFee,

                IsActive = true
            };

            _context.Subjects.Add(subject);

            return subject;
        }

        private async Task<string> GenerateNextClassCodeAsync()
        {
            string? latestCode =
                await _context.ClassSessions
                    .Where(session =>
                        session.ClassCode.StartsWith("CS") &&
                        session.ClassCode.Length == 7)
                    .OrderByDescending(session =>
                        session.ClassCode)
                    .Select(session =>
                        session.ClassCode)
                    .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrWhiteSpace(latestCode) &&
                int.TryParse(
                    latestCode.Substring(2),
                    out int currentNumber))
            {
                nextNumber = currentNumber + 1;
            }

            if (nextNumber > 99999)
            {
                throw new InvalidOperationException(
                    "The maximum Class Code has been reached.");
            }

            return $"CS{nextNumber:D5}";
        }

        private async Task PopulateSelectionsAsync(
            ClassSessionFormViewModel model)
        {
            model.EducationStages = new()
            {
                new SelectListItem
                {
                    Value = "Primary",
                    Text = "Primary",
                    Selected =
                        model.EducationStage ==
                        Models.Enrollment.EducationStage.Primary
                },
                new SelectListItem
                {
                    Value = "Secondary",
                    Text = "Secondary",
                    Selected =
                        model.EducationStage ==
                        Models.Enrollment.EducationStage.Secondary
                }
            };

            var tutors =
                await _userManager.GetUsersInRoleAsync(
                    "Tutor");

            model.Tutors =
                tutors
                    .Where(tutor => tutor.IsActive)
                    .OrderBy(tutor =>
                        tutor.FullName ??
                        tutor.Email)
                    .Select(tutor =>
                        new SelectListItem
                        {
                            Value = tutor.Id,

                            Text =
                                tutor.FullName ??
                                tutor.Email ??
                                "Tutor",

                            Selected =
                                model.TutorUserId ==
                                tutor.Id
                        })
                    .ToList();

            model.Days =
                Enum.GetValues<DayOfWeek>()
                    .Select(day =>
                        new SelectListItem
                        {
                            Value = day.ToString(),
                            Text = day.ToString(),

                            Selected =
                                model.DayOfWeek == day
                        })
                    .ToList();
        }
    }
}