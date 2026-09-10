using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Tutor
{
    public class TutorMyClassesViewModel
    {
        public string TutorName { get; set; } = "Tutor";

        /*
         * Dashboard summary values are based on all classes
         * assigned to the logged-in Tutor.
         */
        public int TotalClassCount { get; set; }

        public int ActiveClassCount { get; set; }

        public int TodayClassCount { get; set; }


        /*
         * Real-time filter values.
         */
        public string? SubjectName { get; set; }

        public DayOfWeek? DayOfWeek { get; set; }

        public ClassSessionStatus? Status { get; set; }


        /*
         * Options used by the Subject dropdown.
         */
        public List<string> SubjectOptions { get; set; }
            = new();


        /*
         * Classes shown after filters are applied.
         */
        public List<TutorMyClassItemViewModel> Classes { get; set; }
            = new();


        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(SubjectName) ||
            DayOfWeek.HasValue ||
            Status.HasValue;
    }


    public class TutorMyClassItemViewModel
    {
        public int ClassSessionId { get; set; }

        public string ClassCode { get; set; }
            = string.Empty;

        public string ClassName { get; set; }
            = string.Empty;

        public string EducationStage { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string SubjectName { get; set; }
            = string.Empty;

        public string Day { get; set; }
            = string.Empty;

        public string StartTime { get; set; }
            = string.Empty;

        public string EndTime { get; set; }
            = string.Empty;

        public string GoogleMeetLink { get; set; }
            = string.Empty;

        public int Capacity { get; set; }

        public int StudentCount { get; set; }

        public ClassSessionStatus Status { get; set; }
    }


    public class TutorClassDetailsViewModel
    {
        public int ClassSessionId { get; set; }

        public string ClassCode { get; set; }
            = string.Empty;

        public string ClassName { get; set; }
            = string.Empty;

        public string EducationStage { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string SubjectName { get; set; }
            = string.Empty;

        public string Day { get; set; }
            = string.Empty;

        public string StartTime { get; set; }
            = string.Empty;

        public string EndTime { get; set; }
            = string.Empty;

        public string GoogleMeetLink { get; set; }
            = string.Empty;

        public int Capacity { get; set; }

        public int StudentCount { get; set; }

        public string? Remark { get; set; }

        public ClassSessionStatus Status { get; set; }

        public bool CanJoinMeeting =>
            Status == ClassSessionStatus.Active &&
            !string.IsNullOrWhiteSpace(GoogleMeetLink);
    }
}