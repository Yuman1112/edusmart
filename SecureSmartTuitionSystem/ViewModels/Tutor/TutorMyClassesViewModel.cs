using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Tutor
{
    public class TutorMyClassesViewModel
    {
        public string TutorName { get; set; } = "Tutor";

        public int TotalClassCount { get; set; }

        public int ActiveClassCount { get; set; }

        public int TodayClassCount { get; set; }

        public List<TutorMyClassItemViewModel> Classes { get; set; }
            = new();
    }

    public class TutorMyClassItemViewModel
    {
        public int ClassSessionId { get; set; }

        public string ClassCode { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public string EducationStage { get; set; } = string.Empty;

        public string AcademicLevel { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string Day { get; set; } = string.Empty;

        public string StartTime { get; set; } = string.Empty;

        public string EndTime { get; set; } = string.Empty;

        public string GoogleMeetLink { get; set; } = string.Empty;

        public decimal MonthlyFee { get; set; }

        public int Capacity { get; set; }

        public int StudentCount { get; set; }

        public ClassSessionStatus Status { get; set; }
    }
}