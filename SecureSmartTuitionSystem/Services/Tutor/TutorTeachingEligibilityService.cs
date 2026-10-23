using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services;

namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorTeachingEligibilityService(ApplicationDbContext db)
{
    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool Matches(
        IEnumerable<TutorTeachingSubject> teachingSubjects,
        EducationStage stage,
        string? academicLevel,
        string? subjectCode)
    {
        string stageName = stage.ToString();
        string level = Normalize(academicLevel);
        string code = Normalize(subjectCode);

        if (!Enum.IsDefined(stage)
            || !TeachingSubjectCatalog.IsValidStageAndLevel(stageName, level)
            || string.IsNullOrEmpty(code))
            return false;

        return teachingSubjects.Any(item =>
            Normalize(item.EducationStage) == Normalize(stageName)
            && Normalize(item.AcademicLevel) == level
            && Normalize(item.SubjectCode) == code);
    }

    public async Task<string?> ValidateAsync(
        string userId,
        EducationStage stage,
        string? academicLevel,
        string? subjectCode)
    {
        bool activeTutor = await db.TutorProfiles.AsNoTracking().AnyAsync(profile =>
            profile.ApplicationUserId == userId
            && profile.IsActive
            && profile.ApplicationUser.IsActive
            && !profile.ApplicationUser.MustChangePassword
            && db.UserRoles.Any(userRole => userRole.UserId == userId
                && db.Roles.Any(role => role.Id == userRole.RoleId
                    && role.NormalizedName == "TUTOR")));

        if (!activeTutor)
            return "The selected tutor account is not available for teaching.";

        var subjects = await db.TutorTeachingSubjects.AsNoTracking()
            .Where(item => item.TutorProfile.ApplicationUserId == userId)
            .ToListAsync();

        if (subjects.Count == 0)
            return "This tutor has no saved Teaching Subjects & Levels. Update the tutor profile before assigning a class.";

        return Matches(subjects, stage, academicLevel, subjectCode)
            ? null
            : $"This tutor is not assigned to teach {Normalize(academicLevel)} - {Normalize(subjectCode)} ({stage}). Update Teaching Subjects & Levels or choose another tutor.";
    }

    public async Task<string?> ValidateSubjectAsync(string userId, int subjectId)
    {
        var subject = await db.Subjects.AsNoTracking()
            .FirstOrDefaultAsync(item => item.SubjectId == subjectId);

        if (subject == null)
            return "The class subject could not be found.";

        return await ValidateAsync(userId, subject.EducationStage,
            subject.AcademicLevel, subject.SubjectName);
    }
}
