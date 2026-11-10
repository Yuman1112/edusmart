using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Tutor;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Tutor;
namespace SecureSmartTuitionSystem.Services.Tutor;
public sealed class TutorSchedulingService(ApplicationDbContext db)
{
    public static string? ValidateTerm(DateTime from, DateTime? end, DateTime now)
    {
        if (from.Year < 2000 || from.Year > 2100 || from.Date < now.Date)
            return "Choose a start date today or later, through 2100.";
        var to = end?.Date ?? AcademicYearPolicy.EndOfYear(from);
        if (to < from.Date || to.Year != from.Year)
            return "The end date must be on or after the start date, within the same academic year.";
        return null;
    }
    public static DateTime? FirstUpcomingLesson(DateTime from, DateTime to, DayOfWeek day, TimeOnly start, DateTime now)
    {
        var date = from.Date.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);
        for (; date <= to.Date; date = date.AddDays(7))
            if (LateMonthEnrollmentPolicy.IsRegularTeachingDate(date) && date.Add(start.ToTimeSpan()) > now)
                return date;
        return null;
    }
    public static bool IsFree(IEnumerable<int> availability, IReadOnlyList<LessonItem> booked,
        DateTime from, DateTime to, DayOfWeek day, TimeOnly start, TimeOnly end, DateTime now,
        IReadOnlyList<ClassSession>? weeklyReservations = null)
    {
        if (ValidateTerm(from, to, now) != null || !Enum.IsDefined(day) || day == DayOfWeek.Sunday
            || start >= end || start < new TimeOnly(8, 0) || end > new TimeOnly(22, 0)
            || end - start < TimeSpan.FromMinutes(30) || end - start > TimeSpan.FromHours(4)
            || !TutorAvailabilityService.Covers(availability, (int)day, start, end)) return false;
        var first = FirstUpcomingLesson(from, to, day, start, now);
        if (!first.HasValue) return false;
        // A one-off cancellation does not release an active recurring class's weekly reservation.
        // Reserve across subjects/levels for the same tutor, but only where actual term dates overlap.
        if (weeklyReservations != null && weeklyReservations.Any(session =>
            session.Status == ClassSessionStatus.Active && ClassScheduleService.WeeklyOverlap(
                first.Value, to, day, start, end, ClassScheduleService.FirstDate(session),
                AcademicYearPolicy.ClassEnd(session), session.DayOfWeek, session.StartTime, session.EndTime))) return false;
        for (var date = first.Value; date <= to.Date; date = date.AddDays(7))
            if (LateMonthEnrollmentPolicy.IsRegularTeachingDate(date) && booked.Any(x => !x.Cancelled && x.Date.Date == date && start < x.End && end > x.Start)) return false;
        return true;
    }
    public static List<TutorWeeklySlot> BuildSlots(IEnumerable<int> availability, IReadOnlyList<LessonItem> booked,
        DateTime from, DateTime to, int durationMinutes, DateTime now,
        IReadOnlyList<ClassSession>? weeklyReservations = null)
    {
        var result = new List<TutorWeeklySlot>();
        if (durationMinutes < 30 || durationMinutes > 240 || ValidateTerm(from, to, now) != null) return result;
        var values = availability.ToHashSet();
        for (var day = 1; day <= 6; day++)
        {
            for (var minutes = 480; minutes + durationMinutes <= 1320; minutes += 30)
            {
                var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes));
                var end = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes + durationMinutes));
                if (!IsFree(values, booked, from, to, (DayOfWeek)day, start, end, now, weeklyReservations)) continue;
                var first = FirstUpcomingLesson(from, to, (DayOfWeek)day, start, now)!.Value;
                int lessonCount = 0;
                for (var date = first; date <= to.Date; date = date.AddDays(7))
                    if (LateMonthEnrollmentPolicy.IsRegularTeachingDate(date)) lessonCount++;
                result.Add(new TutorWeeklySlot($"{day}:{start:HH:mm}:{end:HH:mm}", ((DayOfWeek)day).ToString(), day,
                    start.ToString("HH:mm", CultureInfo.InvariantCulture), end.ToString("HH:mm", CultureInfo.InvariantCulture),
                    first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), lessonCount));
            }
        }
        return result;
    }
    public async Task<List<TutorSchedulingChoice>> EligibleAsync(EducationStage stage, string level, string subject)
    {
        level = level.Trim().ToUpperInvariant(); subject = subject.Trim().ToUpperInvariant();
        var stageName = stage.ToString().ToUpperInvariant();
        return await db.TutorProfiles.AsNoTracking().Where(p => p.IsActive && p.ApplicationUser.IsActive
            && !p.ApplicationUser.MustChangePassword
            && (p.EmploymentType == TutorEmploymentType.FullTime || p.EmploymentType == TutorEmploymentType.PartTime)
            && db.UserRoles.Any(ur => ur.UserId == p.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "TUTOR"))
            && p.TeachingSubjects.Any(s => s.EducationStage.ToUpper() == stageName
                && s.AcademicLevel.ToUpper() == level && s.SubjectCode.ToUpper() == subject))
            .OrderBy(p => p.FullName).Select(p => new TutorSchedulingChoice(p.ApplicationUserId,
                p.FullName + " · " + p.ApplicationUser.UserName)).ToListAsync();
    }
    private Task<List<ClassSession>> WeeklyReservationsAsync(string tutorId, DateTime from, DateTime to) =>
        db.ClassSessions.AsNoTracking().Where(x => x.TutorUserId == tutorId && x.Status == ClassSessionStatus.Active &&
            (!x.EndDate.HasValue || x.EndDate.Value >= from.Date) &&
            (x.StartDate ?? x.CreatedAt.AddHours(8)).Date <= to.Date).ToListAsync();

    public async Task<List<TutorWeeklySlot>> OptionsAsync(string tutorId, DateTime from, DateTime? end, int duration)
    {
        var to = end?.Date ?? AcademicYearPolicy.EndOfYear(from);
        var availability = await new TutorAvailabilityService(db).GetSlotsAsync(tutorId);
        var booked = await new ClassScheduleService(db).GetLessonsAsync(from.Date, to, tutorId);
        var reservations = await WeeklyReservationsAsync(tutorId, from, to);
        return BuildSlots(availability, booked, from, to, duration, ClassScheduleService.Now, reservations);
    }
    // Used again by the existing Create validator, including its Serializable save transaction.
    public async Task<string?> ValidateSelectionAsync(string tutorId, DateTime from, DateTime? end,
        DayOfWeek day, TimeOnly start, TimeOnly finish)
    {
        var termError = ValidateTerm(from, end, ClassScheduleService.Now);
        if (termError != null) return termError;
        var to = end?.Date ?? AcademicYearPolicy.EndOfYear(from);
        var availability = await new TutorAvailabilityService(db).GetSlotsAsync(tutorId);
        var booked = await new ClassScheduleService(db).GetLessonsAsync(from.Date, to, tutorId);
        var reservations = await WeeklyReservationsAsync(tutorId, from, to);
        return IsFree(availability, booked, from, to, day, start, finish, ClassScheduleService.Now, reservations) ? null
            : "The selected weekly time is no longer available for the entire class term. Select another suggested slot.";
    }
}
