using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Student,Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LearningClassesController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? studentId = null, DateTime? date = null, string mode = "month")
    {
        var anchor = date?.Date ?? ClassScheduleService.Today;
        if (anchor.Year < 2000 || anchor.Year > 2100) return BadRequest();
        var model = await BuildModelAsync(studentId, anchor, mode);
        return model == null ? NotFound() : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, DateTime date, int? studentId = null, int? changeId = null, bool replacement = false)
    {
        if (date.Year < 2000 || date.Year > 2100) return BadRequest();
        var model = await BuildModelAsync(studentId, date.Date, "today");
        if (model == null) return NotFound();
        var lesson = model.Lessons.FirstOrDefault(x => x.Class.ClassSessionId == id && x.ChangeId == changeId && x.Replacement == replacement);
        if (lesson == null) return NotFound();
        model.Lessons = new List<LessonItem> { lesson };
        return View(model);
    }
    private async Task<LearningClassesModel?> BuildModelAsync(int? studentId, DateTime anchor, string mode)
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        bool isStudent = User.IsInRole("Student"), isParent = User.IsInRole("Parent");
        var children = await db.StudentProfiles.AsNoTracking().Where(s => s.IsActive &&
            ((isStudent && s.ApplicationUserId == userId) ||
             (isParent && s.ParentStudents.Any(x => x.IsActive && x.ParentProfile.ApplicationUserId == userId))))
            .OrderBy(x => x.StudentNumber).ToListAsync();
        if (studentId.HasValue && !children.Any(x => x.StudentProfileId == studentId.Value)) return null;
        var student = studentId.HasValue ? children.First(x => x.StudentProfileId == studentId.Value) : children.FirstOrDefault();
        mode = mode is "today" or "week" or "month" ? mode : "month";
        var first = new DateTime(anchor.Year, anchor.Month, 1);
        var from = mode == "today" ? anchor : mode == "week" ? anchor.AddDays(-((int)anchor.DayOfWeek + 6) % 7) : first;
        var to = mode == "today" ? from : mode == "week" ? from.AddDays(6) : first.AddMonths(1).AddDays(-1);
        var model = new LearningClassesModel { Children = children, Student = student,
            Calendar = new CalendarModel { Anchor = anchor, From = from, To = to, Mode = mode } };
        if (student == null) return model;
        var today = ClassScheduleService.Today;
        var enrollments = await db.StudentEnrollments.AsNoTracking().Include(x => x.ClassSession).ThenInclude(x => x.Subject)
            .Include(x => x.ClassSession).ThenInclude(x => x.TutorUser)
            .Include(x => x.EnrollmentApplication)
            .Where(x => x.StudentProfileId == student.StudentProfileId).ToListAsync();
        model.NextStartDate = enrollments.Where(x => x.StartDate.Date > today &&
            (x.Status == StudentEnrollmentStatus.Active || x.Status == StudentEnrollmentStatus.PendingDrop))
            .Select(x => (DateTime?)x.StartDate.Date).Min();
        model.Enrollments = enrollments.Where(x => LearningAccessService.EnrolledInMonth(x, today)).OrderBy(x => x.StartDate).ThenBy(x => x.ClassSessionId).ToList();
        // Monthly membership is separate from access: future lessons and Meet links remain locked until enrollment starts.
        var started = enrollments.Where(x => x.StartDate.Date <= today).ToList();
        var ids = started.Select(x => x.ClassSessionId).Distinct().ToList();
        if (ids.Count == 0) return model;
        var changes = await db.ClassLessonChanges.AsNoTracking().Where(x => ids.Contains(x.ClassSessionId) &&
            ((x.OriginalDate >= from && x.OriginalDate <= to) || (x.ReplacementDate >= from && x.ReplacementDate <= to))).ToListAsync();
        var entitledChanges = changes.Where(x => (JsonSerializer.Deserialize<List<int>>(x.StudentIdsJson) ?? new()).Contains(student.StudentProfileId))
            .Select(x => x.ClassLessonChangeId).ToHashSet();
        var classes = started.Select(x => x.ClassSession).DistinctBy(x => x.ClassSessionId).ToList();
        var lessons = ClassScheduleService.BuildLessons(classes, changes, from, to);
        model.Lessons = lessons.Where(x => x.Replacement
            ? entitledChanges.Contains(x.ChangeId!.Value)
            : started.Any(e => e.ClassSessionId == x.Class.ClassSessionId && RegularEntitled(e, x.Date)))
            .OrderBy(x => x.Date).ThenBy(x => x.Start).ToList();
        model.Calendar.Lessons = model.Lessons;
        return model;
    }
    private static bool RegularEntitled(StudentEnrollment e, DateTime date) =>
        e.StartDate.Date <= date.Date && e.StartDate.Year == date.Year &&
        (e.Status == StudentEnrollmentStatus.Active || e.Status == StudentEnrollmentStatus.PendingDrop ||
         e.Status == StudentEnrollmentStatus.Dropped || e.Status == StudentEnrollmentStatus.Completed) &&
        (!e.DropEffectiveDate.HasValue || date.Date < e.DropEffectiveDate.Value.Date) &&
        (!e.EndedAt.HasValue || date.Date < e.EndedAt.Value.Date);
}
