using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Reporting;

public class ReportClassFilter
{
    [EnumDataType(typeof(EducationStage))] public EducationStage? EducationStage { get; set; }
    [StringLength(30)] public string? AcademicLevel { get; set; }
    [Range(1, int.MaxValue)] public int? SubjectId { get; set; }
    [StringLength(100)] public string? TutorSearch { get; set; }
    [StringLength(450)] public string? TutorId { get; set; }
    [EnumDataType(typeof(DayOfWeek))] public DayOfWeek? Day { get; set; }
    [Range(1, int.MaxValue)] public int? ClassId { get; set; }
}

public sealed class ReportClassOptions
{
    public string? TutorSearch { get; set; }
    public List<(string Name, string Label, List<SelectListItem> Items)> Fields { get; } = [];
}

public class ReportPeriodFilter : ReportClassFilter
{
    [Range(2000, 2100)] public int Year { get; set; } = DateTime.UtcNow.AddHours(8).Year;
    [Range(0, 12)] public int Month { get; set; } = DateTime.UtcNow.AddHours(8).Month;
    [Range(1, 12)] public int? EndMonth { get; set; }

    public bool HasActiveFilters
    {
        get
        {
            var now = DateTime.UtcNow.AddHours(8);
            return Year != now.Year || Month != now.Month || End != Start ||
                EducationStage.HasValue || !string.IsNullOrWhiteSpace(AcademicLevel) ||
                SubjectId.HasValue || !string.IsNullOrWhiteSpace(TutorId) ||
                !string.IsNullOrWhiteSpace(TutorSearch) || Day.HasValue || ClassId.HasValue;
        }
    }

    public DateTime Start => new(Year, Math.Max(1, Month), 1);
    public DateTime End => new(Year, Month == 0 ? 12 : Math.Max(Month, EndMonth ?? Month), 1);
}
