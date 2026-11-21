using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Communication;
namespace SecureSmartTuitionSystem.Services.Learning;
// In-app notifications only; no new SMTP dependency or change to submission deadlines.
public sealed class AssessmentReminderWorker(IServiceScopeFactory scopes,ILogger<AssessmentReminderWorker> logger):BackgroundService
{
    public static string? Kind(DateTime nowMyt,DateTime dueMyt,DateTime? submittedUtc)
    {
        if(submittedUtc.HasValue)return submittedUtc.Value.AddHours(8)>=dueMyt?"Late submission recorded":null;
        if(nowMyt>=dueMyt)return "Assignment deadline missed";
        return dueMyt<=nowMyt.AddHours(24)?"Assignment due soon":null;
    }
    public static async Task ProcessAsync(ApplicationDbContext db,CancellationToken ct)
    {
        var now=AssessmentRules.NowMyt;var today=now.Date;var cutoff=now.AddDays(-7);var soon=now.AddHours(24);int after=0;
        while(!ct.IsCancellationRequested)
        {
            var ids=await db.Set<AssessmentParticipant>().AsNoTracking().Where(p=>p.AssessmentParticipantId>after&&p.Assessment.Kind==AssessmentKind.Assignment&&p.Assessment.IsActive&&p.Assessment.PublishedAtUtc!=null&&p.Assessment.OpensAtMyt<=now&&p.Assessment.DueAtMyt>=cutoff&&p.Assessment.DueAtMyt<=soon)
                .OrderBy(p=>p.AssessmentParticipantId).Take(100).Select(p=>p.AssessmentParticipantId).ToListAsync(ct);
            if(ids.Count==0)break;
            foreach(var id in ids)
            {
                // Re-read roster and state inside the transaction. Serializable prevents duplicate
                // notifications from concurrent app instances; a deadlock victim retries next run.
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                var p=await db.Set<AssessmentParticipant>().AsNoTracking().Include(x=>x.Assessment).ThenInclude(a=>a.ClassSession).Include(x=>x.StudentProfile).SingleOrDefaultAsync(x=>x.AssessmentParticipantId==id,ct);
                if(p==null||!p.Assessment.IsActive||p.Assessment.PublishedAtUtc==null||p.Assessment.OpensAtMyt>now||!p.StudentProfile.IsActive){await tx.CommitAsync(ct);continue;}
                var kind=Kind(now,p.Assessment.DueAtMyt,p.SubmittedAtUtc);
                bool enrolled=await db.StudentEnrollments.AnyAsync(e=>e.StudentProfileId==p.StudentProfileId&&e.ClassSessionId==p.Assessment.ClassSessionId&&e.StartDate<=today&&e.StartDate.Year==today.Year&&(e.Status==StudentEnrollmentStatus.Active||e.Status==StudentEnrollmentStatus.PendingDrop)&&(!e.DropEffectiveDate.HasValue||e.DropEffectiveDate>today)&&(!e.EndedAt.HasValue||e.EndedAt>today),ct);
                if(kind==null||!enrolled||p.Assessment.ClassSession.Status!=ClassSessionStatus.Active||p.Assessment.ClassSession.StartDate>today||p.Assessment.ClassSession.EndDate<today){await tx.CommitAsync(ct);continue;}
                var recipients=await db.ParentStudents.Where(l=>l.StudentProfileId==p.StudentProfileId&&l.IsActive&&l.ParentProfile.ApplicationUser.IsActive).Select(l=>l.ParentProfile.ApplicationUserId).ToListAsync(ct);
                if(!string.IsNullOrEmpty(p.StudentProfile.ApplicationUserId)) recipients.Add(p.StudentProfile.ApplicationUserId);
                if(kind!="Assignment due soon")recipients.Add(p.Assessment.ClassSession.TutorUserId);
                string title=$"{kind} · Record {id}";
                var requested=recipients.Where(u=>!string.IsNullOrEmpty(u)).Distinct().ToArray();
                var users=await db.Users.Where(u=>requested.Contains(u.Id)&&u.IsActive).Select(u=>u.Id).ToArrayAsync(ct);
                var existing=await db.UserNotifications.Where(n=>users.Contains(n.UserId)&&n.Title==title).Select(n=>n.UserId).ToListAsync(ct);
                foreach(var user in users.Except(existing))db.UserNotifications.Add(new UserNotification{UserId=user,Title=title,
                    Text=$"{p.StudentNumber} · {p.Assessment.ClassSession.ClassCode} · {p.Assessment.Title}. Deadline: {p.Assessment.DueAtMyt:dd MMM yyyy HH:mm} MYT. "+(kind=="Assignment due soon"?"Submit before the deadline in Learning Progress > Assessments.":kind=="Late submission recorded"?"A late submission is recorded. Review the assessment record.":"No confirmed submission was recorded at the deadline. Contact the tutor; this alert does not reopen submission.")});
                await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            }
            after=ids[^1];
        }
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try{using var scope=scopes.CreateScope();await ProcessAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception){logger.LogError("Assessment reminder processing failed; next run will retry. Check database availability and application logs.");}
            try{await Task.Delay(TimeSpan.FromMinutes(5),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
        }
    }
}
