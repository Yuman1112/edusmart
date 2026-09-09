namespace SecureSmartTuitionSystem.ViewModels.Admin
{
    public class AdminDashboardViewModel
    {
        public int StudentCount { get; set; }

        public int TutorCount { get; set; }

        public int ActiveClassCount { get; set; }

        public int PendingEnrollmentCount { get; set; }

        public List<AdminClassItemViewModel> RecentClasses { get; set; }
            = new();

        public List<PendingEnrollmentItemViewModel>
            PendingEnrollments
        { get; set; } = new();
    }

    public class AdminClassItemViewModel
    {
        public string ClassCode { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string AcademicLevel { get; set; } = string.Empty;

        public string Schedule { get; set; } = string.Empty;
    }

    public class PendingEnrollmentItemViewModel
    {
        public int EnrollmentApplicationId { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public string GuardianName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; }
    }
}