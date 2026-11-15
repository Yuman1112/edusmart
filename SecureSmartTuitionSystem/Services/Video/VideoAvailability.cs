namespace SecureSmartTuitionSystem.Services.Video;
public static class VideoAvailability
{
    public const int ViewingDays = 30;
    public static IQueryable<SecureSmartTuitionSystem.Models.Video.RecordedLesson> ForStudents(
        IQueryable<SecureSmartTuitionSystem.Models.Video.RecordedLesson> recordings, DateTime nowUtc)
    {
        var cutoff = nowUtc.AddDays(-ViewingDays);
        return recordings.Where(x => x.IsActive && x.CreatedAtUtc > cutoff);
    }
    public static DateTime ExpiresAtUtc(DateTime createdAtUtc) => DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc).AddDays(ViewingDays);
    public static bool Expired(DateTime createdAtUtc, DateTime nowUtc) => nowUtc >= ExpiresAtUtc(createdAtUtc);
}
