using SecureSmartTuitionSystem.Models.Security;
namespace SecureSmartTuitionSystem.Services.Security;

public static class SecurityActivityText
{
    // Only fixed, application-owned wording is used for summaries and optional AI input.
    private static readonly Dictionary<string, string> Events = new(StringComparer.Ordinal) {
        ["LoginSucceeded"] = "A user signed in successfully.",
        ["LoginFailed"] = "A sign-in attempt was rejected.",
        ["Logout"] = "A user signed out.",
        ["AccountLocked"] = "An account was temporarily locked after unsuccessful sign-in attempts.",
        ["TwoFactorChallenge"] = "An additional sign-in verification was requested.",
        ["TwoFactorSucceeded"] = "A user completed additional sign-in verification.",
        ["SessionTerminated"] = "A sign-in session was ended by the system.",
        ["RequestRejected"] = "The system rejected a request.",
        ["UploadRejected"] = "A file upload was rejected.",
        ["OperationError"] = "An operation could not be completed and needs review.",
        ["UnhandledError"] = "An unexpected system error needs review.",
        ["TutorRecordsExported"] = "Tutor records were exported.",
        ["AttendanceRecordsExported"] = "Attendance records were exported.",
        ["TutorReactivated"] = "A tutor account was reactivated.",
        ["TutorDeactivated"] = "A tutor account was deactivated."
    };
    private static readonly Dictionary<string, string> Entities = new(StringComparer.Ordinal) {
        ["ApplicationUser"]="user account", ["StudentProfile"]="student profile", ["ParentProfile"]="parent profile",
        ["TutorProfile"]="tutor profile", ["ParentStudent"]="parent-student link", ["ClassSession"]="class session",
        ["Subject"]="subject", ["StudentEnrollment"]="enrollment", ["EnrollmentApplication"]="enrollment application",
        ["EnrollmentApplicationSubject"]="selected class", ["Bill"]="enrollment bill", ["Payment"]="payment",
        ["MonthlyInvoice"]="monthly tuition bill", ["MonthlyInvoiceLine"]="monthly bill item",
        ["BillingReminder"]="billing notification", ["EnrollmentExitRequest"]="withdrawal request",
        ["UserNotification"]="notification", ["Conversation"]="conversation", ["ChatMessage"]="message",
        ["MessageAttachment"]="message attachment", ["AttendanceRecord"]="attendance record"
    };
    public static string Summary(AuditLogEntry entry)
    {
        if (Events.TryGetValue(entry.EventName, out var known)) return known;
        var parts = entry.EventName.Split('.');
        if (parts.Length == 2 && parts[1] is "Added" or "Modified" or "Deleted")
        {
            var entity = Entities.GetValueOrDefault(parts[0], "system record");
            var action = parts[1] switch { "Added" => "created", "Modified" => "updated", _ => "deleted" };
            var article = "aeiou".Contains(entity[0]) ? "An" : "A";
            return $"{article} {entity} was {action}.";
        }
        return "System activity was recorded. Open Details to review it.";
    }
    public static string UserLabel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "System") return "System";
        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^AD\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Administrator";
        return Guid.TryParse(name, out _) ? "System user" : name;
    }
}
