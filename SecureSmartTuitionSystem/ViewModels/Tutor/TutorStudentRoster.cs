namespace SecureSmartTuitionSystem.ViewModels.Tutor;
public sealed record TutorRosterClass(int Id, string Code, string Name);
public sealed record TutorRosterStudent(int Id, string Number, string Name, string ClassCode, string ClassName,
    string Subject, string Level, DateTime StartDate, string Status);
public sealed class TutorStudentRoster
{
    public int? ClassId { get; set; }
    public string Search { get; set; } = "";
    public string Status { get; set; } = "";
    public string Sort { get; set; } = "name_asc";
    public int Page { get; set; } = 1;
    public int PageSize => 25;
    public int Total { get; set; }
    public int StudentCount { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public IReadOnlyList<TutorRosterClass> Classes { get; set; } = [];
    public IReadOnlyList<TutorRosterStudent> Students { get; set; } = [];
    public string SortFor(string key) => Sort == key + "_asc" ? key + "_desc" : key + "_asc";
}
