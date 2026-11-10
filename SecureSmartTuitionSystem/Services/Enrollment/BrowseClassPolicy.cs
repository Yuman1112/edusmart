using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Tutor;
namespace SecureSmartTuitionSystem.Services.Enrollment;
public static class BrowseClassPolicy
{
    public static DateTime? FirstAvailableLesson(ClassSession session, DateTime now)
    {
        if (session.Status != ClassSessionStatus.Active) return null;
        var first = ClassScheduleService.FirstDate(session);
        var from = first > now.Date ? first : now.Date;
        var end = AcademicYearPolicy.ClassEnd(session);
        if (from > end) return null;
        return TutorSchedulingService.FirstUpcomingLesson(from, end, session.DayOfWeek, session.StartTime, now);
    }
}
