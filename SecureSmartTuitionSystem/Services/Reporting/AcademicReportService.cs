using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.ViewModels.Reporting;
namespace SecureSmartTuitionSystem.Services.Reporting;
public sealed class AcademicReportService(ApplicationDbContext db)
{
    // Reuse the academic ownership query. A forged class/subject filter never expands scope.
    public async Task<AcademicReport> BuildAsync(ClaimsPrincipal user, AcademicReportFilter filter, CancellationToken ct=default, int? studentId=null)
    {
        var model=new AcademicReport { Filter=filter };
        var classes=new LearningMaterialService(db).Classes(user);
        var ids = await ReportClassFilters.ApplyAsync(classes, filter, model.ClassOptions, ct);
        var from = filter.Start;
        var to = filter.End.AddMonths(1);
        var now = AssessmentRules.NowMyt;
        // Only published/opened assessments, using due-date year. Exclude drafts and inactive work.
        var assessments=db.Set<Assessment>().AsNoTracking().Where(a=>ids.Contains(a.ClassSessionId)&&a.IsActive&&a.PublishedAtUtc!=null&&a.OpensAtMyt<=now&&a.DueAtMyt>=from&&a.DueAtMyt<to);
        var rows=await assessments.OrderBy(a=>a.DueAtMyt).ThenBy(a=>a.AssessmentId).Select(a=>new { a.AssessmentId,a.Kind,a.Title,a.DueAtMyt,a.ClassSession.ClassCode,a.ClassSession.ClassName,a.ClassSession.DayOfWeek,a.ClassSession.StartTime,a.ClassSession.Subject.SubjectName }).ToListAsync(ct);
        var eligible=assessments.Select(a=>a.AssessmentId);
        var people=await db.Set<AssessmentParticipant>().AsNoTracking().Where(p=>eligible.Contains(p.AssessmentId) && (!studentId.HasValue || p.StudentProfileId == studentId.Value))
            .Select(p=>new AcademicObservation(p.AssessmentId,p.StudentProfileId,p.StudentNumber,p.StudentName,p.SubmittedAtUtc,p.Assessment.DueAtMyt,p.Assessment.ResultsPublishedAtUtc!=null,p.Marks,p.Assessment.MaxMarks,p.StudentProfile.AcademicLevel,p.Assessment.ClassSessionId,p.Assessment.ClassSession.ClassName,p.Assessment.ClassSession.DayOfWeek,p.Assessment.ClassSession.StartTime,p.Assessment.Title,p.Assessment.Kind,p.Feedback)).ToListAsync(ct);
        model.Students=SummarizeStudents(people);
        if (studentId.HasValue)
        {
            model.StudentClasses = people.GroupBy(p => p.ClassId)
                .Select(g =>
                {
                    var first = g.First();
                    var name = ReportClassFilters.DisplayName(first.ClassName, first.Day, first.Start);
                    var schedule = ReportClassFilters.Schedule(first.Day, first.Start);
                    var assessments = g.OrderBy(p => p.Due).ThenBy(p => p.Title).Select(p => new StudentAssessmentDetails(
                        p.Title, p.Kind.ToString(), p.Due, p.SubmittedUtc?.AddHours(8), p.Released ? p.Marks : null,
                        p.Maximum, p.Released, p.Released ? p.Feedback : null)).ToList();
                    return new StudentClassPerformance(name, schedule, SummarizeStudents(g).Single(), assessments);
                })
                .OrderBy(g => g.ClassName).ThenBy(g => g.Schedule).ToList();
            return model;
        }
        var released=people.Where(p=>p.Released&&p.Marks.HasValue&&p.Maximum>0).ToList();
        model.Graded=released.Count;model.Average=released.Count==0?null:released.Average(p=>p.Marks!.Value/p.Maximum*100m);
        var groups=people.GroupBy(p=>p.AssessmentId).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var a in rows.Where(a=>a.Kind==AssessmentKind.Assignment))
        {
            var roster=groups.GetValueOrDefault(a.AssessmentId)??[];
            model.Assignments.Add(SummarizeAssignment(a.AssessmentId,a.Title,a.ClassCode,ReportClassFilters.DisplayName(a.ClassName,a.DayOfWeek,a.StartTime),a.SubjectName,a.DueAtMyt,roster,now)
                with { Schedule = ReportClassFilters.Schedule(a.DayOfWeek,a.StartTime) });
        }
        return model;
    }
    public static List<StudentPerformanceRow> SummarizeStudents(IEnumerable<AcademicObservation> people)=>people.GroupBy(p=>p.StudentId).Select(g=>
    {
        var graded=g.Where(p=>p.Released&&p.Marks.HasValue&&p.Maximum>0).ToList();var first=g.First();
        return new StudentPerformanceRow(g.Key,first.Number,first.Name,g.Count(),g.Count(p=>p.SubmittedUtc.HasValue),graded.Count,graded.Count==0?null:graded.Average(p=>p.Marks!.Value/p.Maximum*100m),FormatLevel(first.Level));
    }).OrderBy(p=>p.StudentNumber,StringComparer.Ordinal).ToList();
    private static string FormatLevel(string level)
    {
        if (level.StartsWith("FORM ", StringComparison.OrdinalIgnoreCase)) return $"Secondary · {level}";
        if (level.StartsWith("YEAR ", StringComparison.OrdinalIgnoreCase)) return $"Primary · {level}";
        return level;
    }

    public static AssignmentRateRow SummarizeAssignment(int id,string title,string code,string name,string subject,DateTime due,IReadOnlyList<AcademicObservation> people,DateTime now)=>
        new(id,title,code,name,subject,due,people.Count,people.Count(p=>p.SubmittedUtc.HasValue),people.Count(p=>p.SubmittedUtc.HasValue&&p.SubmittedUtc.Value.AddHours(8)<due),people.Count(p=>p.SubmittedUtc.HasValue&&p.SubmittedUtc.Value.AddHours(8)>=due),now>=due?people.Count(p=>!p.SubmittedUtc.HasValue):0);
}
public sealed record AcademicObservation(int AssessmentId,int StudentId,string Number,string Name,DateTime? SubmittedUtc,DateTime Due,bool Released,decimal? Marks,decimal Maximum,string Level="",int ClassId=0,string? ClassName=null,DayOfWeek Day=default,TimeOnly Start=default,string Title="",AssessmentKind Kind=default,string? Feedback=null);
