namespace SecureSmartTuitionSystem.ViewModels.Tutor;

public sealed record TutorRosterClass(int Id, string Code, string Name);
public sealed record TutorRosterStudent(string Number, string Name, string ClassCode, string ClassName);
public sealed record TutorStudentRoster(int? ClassId, IReadOnlyList<TutorRosterClass> Classes, IReadOnlyList<TutorRosterStudent> Students);
