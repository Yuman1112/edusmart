namespace SecureSmartTuitionSystem.ViewModels.Parent
{
    public class ParentDashboardViewModel
    {
        public string ParentName { get; set; }
            = string.Empty;

        public string ParentUserName { get; set; }
            = string.Empty;

        public List<ParentDashboardStudentViewModel>
            Students
        { get; set; }
            = new();
    }

    public class ParentDashboardStudentViewModel
    {
        public int StudentProfileId { get; set; }

        public string StudentUserName { get; set; }
            = string.Empty;

        public string StudentName { get; set; }
            = string.Empty;

        public string School { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string Relationship { get; set; }
            = string.Empty;
    }
}