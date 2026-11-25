using SecureSmartTuitionSystem.Models.Attendance;
namespace SecureSmartTuitionSystem.Services.Attendance;
public static class AttendanceConfirmationRules
{
    public static bool WaitingForTutor(AttendanceCorrectionRequest r) => r.Decision == CorrectionDecision.Pending && r.TutorConfirmationRequestedAtUtc.HasValue && !r.TutorConfirmedAtUtc.HasValue;
    public static bool CanApprove(AttendanceCorrectionRequest r) => r.Decision == CorrectionDecision.Pending &&
        (!r.TutorConfirmationRequestedAtUtc.HasValue || (r.TutorConfirmedAtUtc.HasValue && r.TutorSupportsCorrection.HasValue));
    public static bool ReviewRemarkRequired(AttendanceCorrectionRequest r, string decision) =>
        decision != "approve" || !r.TutorConfirmationRequestedAtUtc.HasValue || !r.TutorConfirmedAtUtc.HasValue || r.TutorSupportsCorrection != true;
    public static string Label(AttendanceCorrectionRequest r) => r.Decision != CorrectionDecision.Pending ? r.Decision.ToString() : WaitingForTutor(r) ? "Waiting Tutor Confirmation" : "Pending Admin Review";
}
