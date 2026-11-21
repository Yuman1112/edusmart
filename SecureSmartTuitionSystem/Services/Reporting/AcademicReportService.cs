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
    public async Task<AcademicReport> BuildAsync(ClaimsPrincipal user, AcademicReportFilter filter, CancellationToken ct=default)
    {
        var model=new AcademicReport { Filter=filter };
        var classes=new LearningMaterialService(db).Classes(user);
        model.Subjects=await classes.Select(c=>new { c.SubjectId,c.Subject.SubjectName,c.Subject.AcademicLevel }).Distinct()
            .OrderBy(c=>c.SubjectName).ThenBy(c=>c.AcademicLevel).Select(c=>new SelectListItem(c.SubjectName+" · "+c.AcademicLevel,c.SubjectId.ToString())).ToListAsync(ct);
        if(filter.SubjectId.HasValue) classes=classes.Where(c=>c.SubjectId==filter.SubjectId);
        model.Classes=await classes.OrderBy(c=>c.ClassCode).Select(c=>new SelectListItem(c.ClassCode+" · "+(c.ClassName??""),c.ClassSessionId.ToString())).ToListAsync(ct);
        if(filter.ClassId.HasValue) classes=classes.Where(c=>c.ClassSessionId==filter.ClassId);
        var ids=classes.Select(c=>c.ClassSessionId);
        var from=new DateTime(filter.Year,1,1);var to=from.AddYears(1);var now=AssessmentRules.NowMyt;
        // Only published/opened assessments, using due-date year. Exclude drafts and inactive work.
        var assessments=db.Set<Assessment>().AsNoTracking().Where(a=>ids.Contains(a.ClassSessionId)&&a.IsActive&&a.PublishedAtUtc!=null&&a.OpensAtMyt<=now&&a.DueAtMyt>=from&&a.DueAtMyt<to);
        var rows=await assessments.OrderBy(a=>a.DueAtMyt).ThenBy(a=>a.AssessmentId).Select(a=>new { a.AssessmentId,a.Kind,a.Title,a.DueAtMyt,a.ClassSession.ClassCode,a.ClassSession.ClassName,a.ClassSession.Subject.SubjectName }).ToListAsync(ct);
        var eligible=assessments.Select(a=>a.AssessmentId);
        var people=await db.Set<AssessmentParticipant>().AsNoTracking().Where(p=>eligible.Contains(p.AssessmentId))
            .Select(p=>new AcademicObservation(p.AssessmentId,p.StudentProfileId,p.StudentNumber,p.StudentName,p.SubmittedAtUtc,p.Assessment.DueAtMyt,p.Assessment.ResultsPublishedAtUtc!=null,p.Marks,p.Assessment.MaxMarks)).ToListAsync(ct);
        model.Students=SummarizeStudents(people);
        var released=people.Where(p=>p.Released&&p.Marks.HasValue&&p.Maximum>0).ToList();
        model.Graded=released.Count;model.Average=released.Count==0?null:released.Average(p=>p.Marks!.Value/p.Maximum*100m);
        var groups=people.GroupBy(p=>p.AssessmentId).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var a in rows.Where(a=>a.Kind==AssessmentKind.Assignment))
        {
            var roster=groups.GetValueOrDefault(a.AssessmentId)??[];
            model.Assignments.Add(SummarizeAssignment(a.AssessmentId,a.Title,a.ClassCode,a.ClassName??"",a.SubjectName,a.DueAtMyt,roster,now));
        }
        return model;
    }
    public static List<StudentPerformanceRow> SummarizeStudents(IEnumerable<AcademicObservation> people)=>people.GroupBy(p=>p.StudentId).Select(g=>
    {
        var graded=g.Where(p=>p.Released&&p.Marks.HasValue&&p.Maximum>0).ToList();var first=g.First();
        return new StudentPerformanceRow(g.Key,first.Number,first.Name,g.Count(),g.Count(p=>p.SubmittedUtc.HasValue),graded.Count,graded.Count==0?null:graded.Average(p=>p.Marks!.Value/p.Maximum*100m));
    }).OrderBy(p=>p.StudentNumber,StringComparer.Ordinal).ToList();
    public static AssignmentRateRow SummarizeAssignment(int id,string title,string code,string name,string subject,DateTime due,IReadOnlyList<AcademicObservation> people,DateTime now)=>
        new(id,title,code,name,subject,due,people.Count,people.Count(p=>p.SubmittedUtc.HasValue),people.Count(p=>p.SubmittedUtc.HasValue&&p.SubmittedUtc.Value.AddHours(8)<due),people.Count(p=>p.SubmittedUtc.HasValue&&p.SubmittedUtc.Value.AddHours(8)>=due),now>=due?people.Count(p=>!p.SubmittedUtc.HasValue):0);
}
public sealed record AcademicObservation(int AssessmentId,int StudentId,string Number,string Name,DateTime? SubmittedUtc,DateTime Due,bool Released,decimal? Marks,decimal Maximum);
