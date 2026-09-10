using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class ClassSessionIndexViewModel
    {
        public List<ClassSession> Sessions { get; set; } = new();

        public string? SubjectName { get; set; }
        public EducationStage? EducationStage { get; set; }
        public string? AcademicLevel { get; set; }
        public string? FeeType { get; set; }
        public string? TutorUserId { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public ClassSessionStatus? Status { get; set; }

        public string SortBy { get; set; } = "className";
        public string SortDirection { get; set; } = "asc";

        public List<SelectListItem> Subjects { get; set; } = new();
        public List<SelectListItem> AcademicLevels { get; set; } = new();
        public List<SelectListItem> Tutors { get; set; } = new();

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(SubjectName) ||
            EducationStage.HasValue ||
            !string.IsNullOrWhiteSpace(AcademicLevel) ||
            !string.IsNullOrWhiteSpace(FeeType) ||
            !string.IsNullOrWhiteSpace(TutorUserId) ||
            DayOfWeek.HasValue ||
            Status.HasValue;
    }
}
