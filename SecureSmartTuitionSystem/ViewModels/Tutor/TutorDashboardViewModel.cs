namespace SecureSmartTuitionSystem.ViewModels.Tutor
{
    public class TutorDashboardViewModel
    {
        public string TutorName { get; set; } = "Tutor";

        public int AssignedClassCount { get; set; }

        public int ActiveStudentCount { get; set; }

        public int TodayClassCount { get; set; }

        public List<TutorClassItemViewModel> Classes { get; set; }
            = new();
    }

    public class TutorClassItemViewModel
    {
        public int ClassSessionId { get; set; }

        public string ClassCode { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string AcademicLevel { get; set; } = string.Empty;

        public string Day { get; set; } = string.Empty;

        public string Time { get; set; } = string.Empty;

        public string Venue { get; set; } = string.Empty;

        public int StudentCount { get; set; }
    }
}