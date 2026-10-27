namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class BrowseClassesViewModel
    {
        public string? SelectedStage { get; set; }

        public string? SelectedSubject { get; set; }

        public string? SelectedLevel { get; set; }

        public List<string> Subjects { get; set; } = new();

        public List<string> AcademicLevels { get; set; } = new();

        public List<AvailableClassItemViewModel> Classes { get; set; }
            = new();
    }

    public class AvailableClassItemViewModel
    {
        public int ClassSessionId { get; set; }

        public string ClassCode { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string AcademicLevel { get; set; } = string.Empty;

        public string TutorName { get; set; } = string.Empty;

        public string Day { get; set; } = string.Empty;

        public string StartTime { get; set; } = string.Empty;

        public string EndTime { get; set; } = string.Empty;

        public string Venue { get; set; } = string.Empty;

        public decimal MonthlyFee { get; set; }

        public int Capacity { get; set; }

        public int SeatsRemaining { get; set; }
    }
}