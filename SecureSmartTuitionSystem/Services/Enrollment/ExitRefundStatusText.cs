using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public static class ExitRefundStatusText
{
    public static string Label(ExitRefundStatus status) => status switch
    {
        ExitRefundStatus.NoRefundDue => "No Refund Due",
        ExitRefundStatus.PendingReview => "Pending Review",
        ExitRefundStatus.Processing => "Processing",
        ExitRefundStatus.PendingProvider => "Pending Provider",
        ExitRefundStatus.Completed => "Completed",
        ExitRefundStatus.Rejected => "Rejected",
        ExitRefundStatus.Failed => "Failed",
        _ => status.ToString()
    };
}
