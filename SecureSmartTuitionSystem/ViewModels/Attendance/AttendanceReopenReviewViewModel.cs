using SecureSmartTuitionSystem.Models.Attendance;
namespace SecureSmartTuitionSystem.ViewModels.Attendance;
public sealed class AttendanceReopenReviewViewModel
{
    public int Attempt { get; set; }
    public bool PreviousWindowMissed { get; set; }
    public List<AttendanceReopenHistoryRow> History { get; set; } = [];
}
public sealed class AttendanceReopenHistoryRow
{
    public int Attempt { get; set; }
    public AttendanceReopenRequest Request { get; set; } = null!;
    public string Outcome { get; set; } = "";
}
