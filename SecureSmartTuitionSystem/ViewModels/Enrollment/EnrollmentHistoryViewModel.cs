using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.ViewModels.Enrollment;
public sealed class EnrollmentHistoryViewModel
{
    public List<EnrollmentHistoryRow> Rows { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int RowOffset { get; set; }
    public int TotalRows { get; set; }
    public EnrollmentStatus? Status { get; set; }
}
public sealed class EnrollmentHistoryRow
{
    public string ApplicationNumber { get; set; } = "";
    public string StudentName { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public DateTime RequestedStartDate { get; set; }
    public int DurationMonths { get; set; }
    public EnrollmentStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public List<string> Classes { get; set; } = new();
}
