using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorWorkloadService(ApplicationDbContext db)
{
    public static DateTime MondayOf(DateTime date) =>
        date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static void Calculate(
        TutorWorkloadRowViewModel row, IEnumerable<LessonItem> lessons)
    {
        var teaching = lessons.Where(lesson =>
            lesson.Class.TutorUserId == row.UserId && !lesson.Cancelled
            && lesson.End > lesson.Start).ToList();
        row.ClassCount = teaching.Select(lesson => lesson.Class.ClassSessionId).Distinct().Count();
        row.RegularLessonCount = teaching.Count(lesson => !lesson.Replacement);
        row.ReplacementCount = teaching.Count(lesson => lesson.Replacement);
        row.TeachingHours = teaching.Sum(lesson =>
            (decimal)(lesson.End.Ticks - lesson.Start.Ticks) / TimeSpan.TicksPerHour);
    }

    public async Task<TutorWorkloadViewModel?> GetAsync(DateTime date, string? userId)
    {
        userId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        var tutors = await db.TutorProfiles.AsNoTracking()
            .OrderBy(profile => profile.ApplicationUser.UserName)
            .Select(profile => new TutorWorkloadRowViewModel
            {
                UserId = profile.ApplicationUserId,
                TutorId = profile.ApplicationUser.UserName ?? "",
                FullName = profile.FullName,
                IsActive = profile.IsActive && profile.ApplicationUser.IsActive
            }).ToListAsync();
        if (userId != null && !tutors.Any(tutor => tutor.UserId == userId)) return null;

        var model = new TutorWorkloadViewModel
        {
            WeekStart = MondayOf(date),
            SelectedUserId = userId,
            AllTutors = tutors,
            Rows = tutors.Where(tutor => userId == null || tutor.UserId == userId).ToList()
        };
        // Reuse the Calendar's dated cancellation and replacement rules.
        var lessons = await new ClassScheduleService(db)
            .GetLessonsAsync(model.WeekStart, model.WeekEnd, userId);
        model.Lessons = lessons.Where(lesson => !lesson.Cancelled && lesson.End > lesson.Start)
            .OrderBy(lesson => lesson.Date).ThenBy(lesson => lesson.Start).ToList();
        var byTutor = model.Lessons.ToLookup(lesson => lesson.Class.TutorUserId);
        foreach (var row in model.Rows) Calculate(row, byTutor[row.UserId]);
        return model;
    }
}