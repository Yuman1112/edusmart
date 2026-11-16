namespace SecureSmartTuitionSystem.ViewModels.Student
{
    public class StudentDashboardViewModel
    {
        public string StudentName { get; set; }
            = string.Empty;

        public string StudentUserName { get; set; }
            = string.Empty;

        public string School { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public int ActiveClassCount { get; set; }
        public DateTime Today { get; set; }
        public DateTime? NextStartDate { get; set; }
        public int TodayClassCount => TodayLessons.Count(x => !x.Cancelled);
        public int AssessmentsDueCount { get; set; }
        public List<SecureSmartTuitionSystem.ViewModels.Enrollment.LessonItem> TodayLessons { get; set; } = new();
        public List<StudentDashboardAssessment> UpcomingAssessments { get; set; } = new();


        public List<StudentDashboardClassViewModel>
            Classes
        { get; set; }
            = new();
    }

    public class StudentDashboardClassViewModel
    {
        public int ClassSessionId { get; set; }
        public string ClassCode { get; set; }
            = string.Empty;

        public string ClassName { get => SecureSmartTuitionSystem.Services.Enrollment.ClassNameFormatter.Clean(_className); set => _className = value; }
        private string _className = "";

        public string SubjectName { get; set; }
            = string.Empty;

        public string Day { get; set; }
            = string.Empty;

        public string StartTime { get; set; }
            = string.Empty;

        public string EndTime { get; set; }
            = string.Empty;
    }
}
namespace SecureSmartTuitionSystem.ViewModels.Student
{
    public sealed class StudentDashboardAssessment
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime DueAtMyt { get; set; }
    }
}
