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

        public List<StudentDashboardClassViewModel>
            Classes
        { get; set; }
            = new();
    }

    public class StudentDashboardClassViewModel
    {
        public string ClassCode { get; set; }
            = string.Empty;

        public string ClassName { get; set; }
            = string.Empty;

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