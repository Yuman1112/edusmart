using System.Globalization;
using SecureSmartTuitionSystem.Models.Enrollment;
namespace SecureSmartTuitionSystem.Services.Enrollment;

public static class ClassCompletionPolicy
{
    // now and lesson dates use Malaysia local calendar/time, as does ClassScheduleService.
    public static string? BlockReason(ClassSession session, DateTime now, IReadOnlyCollection<ClassLessonChange> changes,
        bool hasApprovedUnpaid, bool hasPaidAwaitingFulfillment)
    {
        if (session.Status != ClassSessionStatus.Active) return "Only an active class can be marked as completed.";
        if (!session.EndDate.HasValue) return "This class has no term end date. Open Edit Class and set its actual end date before completing it.";
        var end = session.EndDate.Value.Date;
        if (end >= now.Date) return $"The term ends on {end.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}. This class can be completed from {end.AddDays(1).ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} MYT, after all replacements have ended.";
        var missing = changes.FirstOrDefault(x => !x.ReplacementDate.HasValue || !x.ReplacementStartTime.HasValue || !x.ReplacementEndTime.HasValue || x.ReplacementEndTime.GetValueOrDefault() <= x.ReplacementStartTime.GetValueOrDefault());
        if (missing != null) return $"The cancelled lesson on {missing.OriginalDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} has a missing or invalid replacement schedule. Open Calendar and set its replacement date and time.";
        if (changes.Count > 0) {
            var latest = changes.Max(x => x.ReplacementDate!.Value.Date.Add(x.ReplacementEndTime!.Value.ToTimeSpan()));
            if (latest > now) return $"A replacement lesson has not ended. The last replacement ends on {latest.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)} MYT. Complete this class after that time.";
        }
        if (hasPaidAwaitingFulfillment) return "A confirmed payment is awaiting enrollment creation. Finish enrollment fulfillment before completing this class.";
        if (hasApprovedUnpaid) return "An approved application is still awaiting payment. Complete its payment or cancel the unpaid application before completing this class.";
        return null;
    }
}
