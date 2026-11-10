using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers;

public class ClassesController(ApplicationDbContext context) : Controller
{
    private static readonly string[] PrimaryLevels = ["YEAR 1", "YEAR 2", "YEAR 3", "YEAR 4", "YEAR 5", "YEAR 6"];
    private static readonly string[] SecondaryLevels = ["FORM 1", "FORM 2", "FORM 3", "FORM 4", "FORM 5"];

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Index(string? subject, string? level, string? stage, int? year = null)
    {
        string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        subject = Normalize(subject);
        level = Normalize(level);
        stage = Normalize(stage);
        EducationStage? selectedStage = stage switch { "PRIMARY" => EducationStage.Primary, "SECONDARY" => EducationStage.Secondary, _ => null };
        stage = selectedStage?.ToString();
        var levels = selectedStage switch
        {
            EducationStage.Primary => PrimaryLevels.ToList(),
            EducationStage.Secondary => SecondaryLevels.ToList(),
            _ => PrimaryLevels.Concat(SecondaryLevels).ToList()
        };
        // A stale URL or a change in stage must not retain an incompatible level.
        if (level != null && !levels.Contains(level, StringComparer.OrdinalIgnoreCase)) { level = null; subject = null; }
        var now = ClassScheduleService.Now;
        var today = now.Date;
        // Browsing is a catalogue: future terms must not disappear behind today's date.
        // Enrollment still checks the user's requested start date independently.
        var query = context.ClassSessions.AsNoTracking().Where(c =>
            c.Status == ClassSessionStatus.Active && c.Subject.IsActive &&
            (!c.EndDate.HasValue || c.EndDate.Value.Date >= today));
        var years = await query.Select(c => (c.StartDate ?? c.CreatedAt.AddHours(8)).Year)
            .Distinct().OrderBy(y => y).ToListAsync();
        if (year.HasValue)
            query = query.Where(c => (c.StartDate ?? c.CreatedAt.AddHours(8)).Year == year.Value);
        if (selectedStage.HasValue) query = query.Where(c => c.Subject.EducationStage == selectedStage.Value);
        if (level != null) query = query.Where(c => c.Subject.AcademicLevel.ToUpper() == level);
        // Determine subjects before applying the subject filter, so changing subject remains possible.
        var rows = await query.Select(c => new
        {
            Class = c, c.ClassSessionId, c.ClassCode, c.Capacity, c.DayOfWeek, c.StartTime, c.EndTime,
            c.Subject.SubjectName, c.Subject.AcademicLevel, c.Subject.MonthlyFee,
            TutorName = c.TutorUser.FullName ?? c.TutorUser.Email ?? "Tutor" 
        }).ToListAsync();
        var firstLessons = rows.ToDictionary(x => x.ClassSessionId, x => BrowseClassPolicy.FirstAvailableLesson(x.Class, now));
        rows = rows.Where(x => firstLessons[x.ClassSessionId].HasValue && firstLessons[x.ClassSessionId]!.Value <= today.AddYears(1)).ToList();
        var availability = await EnrollmentAvailability.LoadAsync(context, rows.Select(x => x.ClassSessionId).ToList());
        var occupied = rows.ToDictionary(x => x.ClassSessionId, x => availability.PeakSeats(x.ClassSessionId,
            firstLessons[x.ClassSessionId]!.Value, AcademicYearPolicy.ClassEnd(x.Class)));
        rows = rows.Where(x => occupied[x.ClassSessionId] < x.Capacity).ToList();
        var subjects = rows.Select(x => x.SubjectName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var order = PrimaryLevels.Concat(SecondaryLevels).Select((v, i) => new { v, i }).ToDictionary(x => x.v, x => x.i, StringComparer.OrdinalIgnoreCase);
        var classes = rows.Where(x => subject == null || string.Equals(x.SubjectName.Trim(), subject, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => order.GetValueOrDefault(x.AcademicLevel, int.MaxValue))
            .ThenBy(x => x.SubjectName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DayOfWeek).ThenBy(x => x.StartTime)
            .Select(x => new AvailableClassItemViewModel
            {
                ClassSessionId = x.ClassSessionId, ClassCode = x.ClassCode, SubjectName = x.SubjectName,
                AcademicLevel = x.AcademicLevel, TutorName = x.TutorName, Day = x.DayOfWeek.ToString(),
                StartTime = x.StartTime.ToString("hh:mm tt"), EndTime = x.EndTime.ToString("hh:mm tt"),
                Venue = "Online class", MonthlyFee = x.MonthlyFee, Capacity = x.Capacity, SeatsRemaining = x.Capacity - occupied[x.ClassSessionId]
            }).ToList();
        return View(new BrowseClassesViewModel { SelectedYear = year, Years = years, SelectedStage = stage, SelectedLevel = level, SelectedSubject = subject,
            AcademicLevels = levels, Subjects = subjects, Classes = classes });
    }
}
