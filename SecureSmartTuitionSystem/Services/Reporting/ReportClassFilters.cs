using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Reporting;

namespace SecureSmartTuitionSystem.Services.Reporting;

public static class ReportClassFilters
{
    public static async Task<int[]> ApplyAsync(IQueryable<ClassSession> classes, ReportClassFilter filter,
        ReportClassOptions options, CancellationToken ct = default)
    {
        var scope = await classes.AsNoTracking().Select(c => new
        {
            c.ClassSessionId, c.ClassName, c.DayOfWeek, c.StartTime, c.SubjectId,
            c.Subject.AcademicLevel, c.Subject.EducationStage, c.Subject.SubjectName,
            c.TutorUserId, Tutor = c.TutorUser.FullName ?? c.TutorUser.Email ?? "Tutor"
        }).ToListAsync(ct);

        var candidates = scope.AsEnumerable();
        IEnumerable<T> Choose<T>(IEnumerable<T> rows, string name, string label, string? selected,
            Func<T, string> value, Func<T, string> title, Action clear)
        {
            var items = rows.GroupBy(value).Select(g => new SelectListItem(title(g.First()), g.Key, g.Key == selected))
                .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase).ToList();
            options.Fields.Add((name, label, items));
            if (string.IsNullOrEmpty(selected)) return rows;
            if (items.Any(item => item.Selected)) return rows.Where(row => value(row) == selected);
            clear();
            return rows;
        }

        candidates = Choose(candidates, nameof(filter.EducationStage), "Education Stage", filter.EducationStage?.ToString(),
            c => c.EducationStage.ToString(), c => c.EducationStage.ToString(), () => filter.EducationStage = null);
        candidates = Choose(candidates, nameof(filter.AcademicLevel), "Academic Level", filter.AcademicLevel,
            c => c.AcademicLevel, c => c.AcademicLevel, () => filter.AcademicLevel = null);
        candidates = Choose(candidates, nameof(filter.SubjectId), "Subject", filter.SubjectId?.ToString(),
            c => c.SubjectId.ToString(), c => c.SubjectName, () => filter.SubjectId = null);
        options.TutorSearch = filter.TutorSearch?.Trim();
        if (!string.IsNullOrEmpty(options.TutorSearch))
            candidates = candidates.Where(c => c.Tutor.Contains(options.TutorSearch, StringComparison.OrdinalIgnoreCase));
        candidates = Choose(candidates, nameof(filter.TutorId), "Tutor", filter.TutorId,
            c => c.TutorUserId, c => c.Tutor, () => filter.TutorId = null);
        candidates = Choose(candidates, nameof(filter.Day), "Day", filter.Day?.ToString(),
            c => c.DayOfWeek.ToString(), c => c.DayOfWeek.ToString(), () => filter.Day = null);
        candidates = Choose(candidates, nameof(filter.ClassId), "Class", filter.ClassId?.ToString(),
            c => c.ClassSessionId.ToString(), c => $"{DisplayName(c.ClassName, c.DayOfWeek, c.StartTime)} · {Schedule(c.DayOfWeek, c.StartTime)}", () => filter.ClassId = null);
        return candidates.Select(c => c.ClassSessionId).ToArray();
    }

    public static string Schedule(DayOfWeek day, TimeOnly start) =>
        $"{day} - {start.ToString("hh:mm tt", CultureInfo.InvariantCulture)}";

    public static string DisplayName(string? className, DayOfWeek day, TimeOnly start)
    {
        var title = className ?? "Unnamed Class";
        var suffix = " - " + Schedule(day, start);
        return title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? title[..^suffix.Length] : title;
    }
}
