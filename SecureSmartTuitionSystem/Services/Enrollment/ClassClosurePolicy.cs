using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public static class ClassClosurePolicy
{
    // Closure archives an unoccupied class; it is not an administrative student withdrawal.
    public static string? BlockReason(ClassSessionStatus status, bool hasLiveEnrollments,
        bool hasPendingApplications, bool hasOutstandingReplacements, bool hasUnsettledRefunds = false)
    {
        if (status != ClassSessionStatus.Active) return "Only an active class can be closed.";
        if (hasLiveEnrollments) return "This class still has active or pending-withdrawal students. Resolve their enrollments and any unused paid tuition before closing the class. Closing does not withdraw students or issue refunds.";
        if (hasPendingApplications) return "This class has pending, approved or paid applications awaiting enrollment. Resolve these applications before closing; a paid application must be fulfilled or handled through the payment/refund process.";
        if (hasUnsettledRefunds) return "Resolve outstanding withdrawal refunds before closing the class.";
        if (hasOutstandingReplacements) return "Complete all outstanding replacement lessons before closing the class.";
        return null;
    }
}
