using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Progress;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Services.Learning;
using SecureSmartTuitionSystem.ViewModels.Progress;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
namespace SecureSmartTuitionSystem.Services.Progress;
public static class ProgressRules
{
    public static bool ValidPercent(int percent)=>percent is 0 or 25 or 50 or 75 or 100;
    public static string? Transition(int before,int after)=>!ValidPercent(after)?"Choose 0, 25, 50, 75 or 100 percent.":before==100&&after!=100?"Completed subchapters are locked. Ask Admin to reopen the item.":after<before?"Progress cannot move backwards. Ask Admin to correct the record.":null;
    public static DateTime Deadline(DateTime date,TimeOnly end)=>date.Date.Add(end.ToTimeSpan()).AddHours(2);
    public static bool Late(DateTime date,TimeOnly end,DateTime now)=>now>Deadline(date,end);
    public static bool Version(byte[]? current,string? supplied)=>current==null?string.IsNullOrEmpty(supplied):Convert.ToBase64String(current)==supplied;
    public static decimal? Average(IEnumerable<int> values,int total)=>total==0?null:values.Sum(x=>(decimal)x)/total;
    public static bool Same(ProgressLessonRecord r,LessonItem l)=>r.LessonDate==l.Date.Date&&r.ChangeKey==(l.Replacement?l.ChangeId??-1:0)&&r.OriginalDate==l.OriginalDate.Date&&r.Start==l.Start&&r.End==l.End&&r.TutorUserId==l.Class.TutorUserId;
}
public sealed class ClassProgressService(ApplicationDbContext db)
{
    private static string Own(ClaimsPrincipal u)=>LearningMaterialService.UserId(u);
    public IQueryable<ClassSession> Classes(ClaimsPrincipal u)=>u.IsInRole("Admin")||LearningMaterialService.IsTutor(u)?new LearningMaterialService(db).Classes(u):db.ClassSessions.Where(c=>false);
    private static int Year(ClassSession c)=>ClassScheduleService.FirstDate(c).Year;
    public async Task<ProgressIndex> IndexAsync(ClaimsPrincipal user,ProgressFilter f)
    {
        var model=new ProgressIndex{Filter=f};
        var classes=Classes(user).Where(c=>(c.StartDate??c.CreatedAt.AddHours(8)).Year==f.Year);
        model.Subjects=await classes.Select(c=>new {c.SubjectId,c.Subject.SubjectName,c.Subject.AcademicLevel}).Distinct().OrderBy(c=>c.SubjectName).ThenBy(c=>c.AcademicLevel).Select(c=>new SelectListItem(c.SubjectName+" · "+c.AcademicLevel,c.SubjectId.ToString())).ToListAsync();
        if(f.SubjectId.HasValue)classes=classes.Where(c=>c.SubjectId==f.SubjectId);
        model.Classes=await classes.OrderBy(c=>c.ClassCode).Select(c=>new SelectListItem(c.ClassCode+" · "+(c.ClassName??""),c.ClassSessionId.ToString())).ToListAsync();
        if(f.ClassId.HasValue)classes=classes.Where(c=>c.ClassSessionId==f.ClassId);
        var list=await classes.Include(c=>c.Subject).Include(c=>c.TutorUser).OrderBy(c=>c.ClassCode).ToListAsync();
        var ids=list.Select(c=>c.ClassSessionId).ToList();var subjectIds=list.Select(c=>c.SubjectId).ToList();
        var syllabuses=await db.Set<ProgressSyllabus>().AsNoTracking().Include(s=>s.Topics).Where(s=>subjectIds.Contains(s.SubjectId)&&s.AcademicYear==f.Year).ToListAsync();
        var progress=await db.Set<ProgressTopicState>().AsNoTracking().Where(p=>ids.Contains(p.ClassSessionId)).ToListAsync();
        var from=new DateTime(f.Year,f.Month,1);var to=from.AddMonths(1).AddDays(-1);var now=ClassScheduleService.Now;
        var changes=await db.ClassLessonChanges.AsNoTracking().Where(c=>ids.Contains(c.ClassSessionId)&&((c.OriginalDate>=from&&c.OriginalDate<=to)||(c.ReplacementDate>=from&&c.ReplacementDate<=to))).ToListAsync();
        var records=await db.Set<ProgressLessonRecord>().AsNoTracking().Where(r=>ids.Contains(r.ClassSessionId)&&r.LessonDate>=from&&r.LessonDate<=to).ToListAsync();
        foreach(var c in list)
        {
            var syllabus=syllabuses.SingleOrDefault(s=>s.SubjectId==c.SubjectId);var count=syllabus?.Topics.Count??0;
            var topics=syllabus?.Topics.Select(t=>t.Id).ToHashSet()??[];var values=progress.Where(p=>p.ClassSessionId==c.ClassSessionId&&topics.Contains(p.TopicId)).ToList();
            var lessons=ClassScheduleService.BuildLessons([c],changes.Where(x=>x.ClassSessionId==c.ClassSessionId).ToList(),from,to).Where(l=>!l.Cancelled&&syllabus!=null&&l.Date>=syllabus.TrackingFrom&&l.Date.Add(l.End.ToTimeSpan())<=now).ToList();
            var saved=lessons.Select(l=>(Lesson:l,Record:records.FirstOrDefault(r=>r.ClassSessionId==c.ClassSessionId&&ProgressRules.Same(r,l)))).ToList();
            var schedule = $"{c.DayOfWeek} - {c.StartTime.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)}";
            var suffix = " - " + schedule;
            var name = c.ClassName ?? "Unnamed Class";
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) name = name[..^suffix.Length];
            var level = $"{c.Subject.EducationStage} · {c.Subject.AcademicLevel}";
            model.Rows.Add(new(c.ClassSessionId,c.ClassCode,name,schedule,c.Subject.SubjectName,level,c.TutorUser.FullName??c.TutorUser.Email??"Tutor",count,values.Count(p=>p.Percent==100),ProgressRules.Average(values.Select(p=>p.Percent),count),lessons.Count,saved.Count(x=>x.Record!=null),saved.Count(x=>x.Record==null&&ProgressRules.Late(x.Lesson.Date,x.Lesson.End,now)),saved.Count(x=>x.Record?.SubmittedLate==true)));
        }
        return model;
    }
    public async Task<ProgressDetails?> DetailsAsync(ClaimsPrincipal user,int id,int month,DateTime? date=null,int key=0,int historyPage=1)
    {
        var c=await Classes(user).Include(c=>c.Subject).Include(c=>c.TutorUser).SingleOrDefaultAsync(c=>c.ClassSessionId==id);if(c==null)return null;
        var syllabus=await db.Set<ProgressSyllabus>().AsNoTracking().Include(s=>s.Topics).SingleOrDefaultAsync(s=>s.SubjectId==c.SubjectId&&s.AcademicYear==Year(c));
        var from=new DateTime(Year(c),month,1);var to=from.AddMonths(1).AddDays(-1);
        var model=new ProgressDetails{Class=c,Syllabus=syllabus,Month=month,State=await db.Set<ProgressClassState>().AsNoTracking().SingleOrDefaultAsync(s=>s.ClassSessionId==id)};
        model.Progress=await db.Set<ProgressTopicState>().AsNoTracking().Where(p=>p.ClassSessionId==id).ToListAsync();
        model.Records=await db.Set<ProgressLessonRecord>().AsNoTracking().Where(r=>r.ClassSessionId==id&&r.LessonDate>=from&&r.LessonDate<=to).ToListAsync();
        var history=db.Set<ProgressChange>().AsNoTracking().Where(h=>h.ClassSessionId==id);
        if(date.HasValue)history=history.Where(h=>h.LessonRecord!=null&&h.LessonRecord.LessonDate==date.Value.Date&&h.LessonRecord.ChangeKey==key);
        model.HistoryTotal=await history.CountAsync();model.HistoryPage=Math.Clamp(historyPage,1,model.HistoryPages);
        model.History=await history.Include(h=>h.Topic).Include(h=>h.LessonRecord).OrderByDescending(h=>h.RecordedAtUtc).ThenByDescending(h=>h.Id).Skip((model.HistoryPage-1)*25).Take(25).ToListAsync();
        var actorIds=model.History.Select(h=>h.ActorUserId).Distinct().ToList();
        model.Actors=await db.Users.AsNoTracking().Where(u=>actorIds.Contains(u.Id)).ToDictionaryAsync(u=>u.Id,u=>(u.UserName??"Account")+" · "+(u.FullName??""));
        model.Lessons=(await new ClassScheduleService(db).GetLessonsAsync(from,to,classId:id)).Where(l=>!l.Cancelled&&syllabus!=null&&l.Date>=syllabus.TrackingFrom).ToList();
        if(date.HasValue)model.Selected=model.Lessons.SingleOrDefault(l=>l.Date==date.Value.Date&&(l.Replacement?l.ChangeId:0)==key);
        var selected=model.Selected;var now=ClassScheduleService.Now;
        var existing=selected==null?null:model.Records.SingleOrDefault(r=>r.LessonDate==selected.Date&&r.ChangeKey==key);
        model.CanSave=LearningMaterialService.IsTutor(user)&&c.Status==ClassSessionStatus.Active&&selected!=null&&selected.Date.Add(selected.End.ToTimeSpan())<=now&&(existing==null||ProgressRules.Same(existing,selected));
        model.IsLate=selected!=null&&ProgressRules.Late(selected.Date,selected.End,now);
        model.Form=new(){ClassId=id,Date=selected?.Date??from,ChangeKey=key,Version=model.State==null?null:Convert.ToBase64String(model.State.RowVersion),Notes=existing?.Notes??"",Topics=(syllabus?.Topics.OrderBy(t=>t.Chapter).ThenBy(t=>t.Subchapter).Select(t=>new TopicInput{TopicId=t.Id,Percent=model.Progress.FirstOrDefault(p=>p.TopicId==t.Id)?.Percent??0}).ToList())??[]};
        return model;
    }
    public async Task<SyllabusForm?> SyllabusAsync(ClaimsPrincipal user,int id)
    {
        if(!user.IsInRole("Admin"))return null;
        var c=await Classes(user).Include(c=>c.Subject).SingleOrDefaultAsync(c=>c.ClassSessionId==id);if(c==null)return null;
        var s=await db.Set<ProgressSyllabus>().AsNoTracking().Include(s=>s.Topics).SingleOrDefaultAsync(s=>s.SubjectId==c.SubjectId&&s.AcademicYear==Year(c));
        var model=new SyllabusForm{ClassId=id,ClassLabel=c.Subject.SubjectName+" · "+c.Subject.AcademicLevel+" · "+Year(c),TrackingFrom=s?.TrackingFrom??(ClassScheduleService.Today.Year==Year(c)?ClassScheduleService.Today:new DateTime(Year(c),1,1)),Version=s==null?null:Convert.ToBase64String(s.RowVersion),Locked=s!=null&&await db.Set<ProgressClassState>().AnyAsync(x=>x.SyllabusId==s.Id)};
        for(int i=1;i<=20;i++){var topics=s?.Topics.Where(t=>t.Chapter==i).OrderBy(t=>t.Subchapter).ToList();model.Chapters.Add(new(){Title=topics?.FirstOrDefault()?.ChapterTitle??(s==null&&i<=5?"Chapter "+i:""),Subchapters=topics?.Count>0?string.Join("\n",topics.Select(t=>t.Title)):s==null&&i<=5?"Subchapter 1\nSubchapter 2\nSubchapter 3\nSubchapter 4":""});}
        return model;
    }
    public async Task<string?> SaveSyllabusAsync(ClaimsPrincipal user,SyllabusForm form)
    {
        if(!user.IsInRole("Admin"))return "Access denied.";
        if(form.Chapters==null||form.Chapters.Count is <1 or >20)return "Use 1–20 chapters.";
        var definitions=new List<ProgressTopic>();
        for(int i=0;i<form.Chapters.Count;i++)
        {
            var chapter=form.Chapters[i];var title=chapter.Title?.Trim()??"";var subs=(chapter.Subchapters??"").Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0).ToArray();
            if(title.Length==0&&subs.Length==0)continue;
            if(title.Length is <1 or >100||subs.Length is <1 or >20||subs.Any(s=>s.Length>100))return "Each chapter needs a title and 1–20 subchapter names, each at most 100 characters.";
            for(int j=0;j<subs.Length;j++)definitions.Add(new(){Chapter=i+1,Subchapter=j+1,ChapterTitle=title,Title=subs[j]});
        }
        if(definitions.Count==0)return "Configure at least one chapter and subchapter.";
        return await Write(async()=>{
            var c=await Classes(user).SingleOrDefaultAsync(c=>c.ClassSessionId==form.ClassId);if(c==null)return "Class unavailable.";
            if(form.TrackingFrom.Year!=Year(c))return "Tracking start must be within this academic year.";
            var s=await db.Set<ProgressSyllabus>().Include(s=>s.Topics).SingleOrDefaultAsync(s=>s.SubjectId==c.SubjectId&&s.AcademicYear==Year(c));
            if(!ProgressRules.Version(s?.RowVersion,form.Version))return "The outline changed. Reload before saving.";
            if(s!=null&&await db.Set<ProgressClassState>().AnyAsync(x=>x.SyllabusId==s.Id))return "This subject outline is locked because teaching progress has started.";
            if(s==null){s=new(){SubjectId=c.SubjectId,AcademicYear=Year(c)};db.Add(s);}else{db.RemoveRange(s.Topics);s.Topics.Clear();await db.SaveChangesAsync();}
            s.TrackingFrom=form.TrackingFrom.Date;s.UpdatedAtUtc=DateTime.UtcNow;s.Topics=definitions;await db.SaveChangesAsync();return null;
        });
    }
    public async Task<string?> SaveLessonAsync(ClaimsPrincipal user,LessonProgressForm form)
    {
        if(!LearningMaterialService.IsTutor(user))return "Access denied.";
        if(form.Topics==null||form.Topics.Count is <1 or >400||form.Topics.Select(t=>t.TopicId).Distinct().Count()!=form.Topics.Count)return "Invalid chapter selections. Reload.";
        if(string.IsNullOrWhiteSpace(form.Notes)||form.Notes.Trim().Length is <3 or >500||form.LateReason?.Length>500)return "Enter a lesson summary of 3–500 characters.";
        return await Write(async()=>{
            var c=await Classes(user).SingleOrDefaultAsync(c=>c.ClassSessionId==form.ClassId);if(c==null||c.Status!=ClassSessionStatus.Active)return "Class unavailable.";
            var s=await db.Set<ProgressSyllabus>().Include(s=>s.Topics).SingleOrDefaultAsync(s=>s.SubjectId==c.SubjectId&&s.AcademicYear==Year(c));if(s==null||form.Date.Year!=Year(c)||form.Date.Date<s.TrackingFrom)return "Choose a tracked lesson in this academic year.";
            var lessons=await new ClassScheduleService(db).GetLessonsAsync(form.Date.Date,form.Date.Date,Own(user),c.ClassSessionId);
            var lesson=lessons.SingleOrDefault(l=>!l.Cancelled&&(l.Replacement?l.ChangeId:0)==form.ChangeKey);if(lesson==null)return "This lesson was cancelled, rescheduled or is unavailable. Reload the schedule.";
            var now=ClassScheduleService.Now;if(now<lesson.Date.Add(lesson.End.ToTimeSpan()))return "Update progress only after the lesson has ended.";
            bool late=ProgressRules.Late(lesson.Date,lesson.End,now);if(late&&(form.LateReason??"").Trim().Length<10)return "This update is after the 2-hour deadline. Enter a reason of 10–500 characters.";
            var state=await db.Set<ProgressClassState>().SingleOrDefaultAsync(x=>x.ClassSessionId==form.ClassId);
            if(!ProgressRules.Version(state?.RowVersion,form.Version))return "Progress changed in another tab. Reload before saving.";
            if(state!=null&&state.SyllabusId!=s.Id)return "The class outline has changed. Contact Admin.";
            if(!s.Topics.Select(t=>t.Id).Order().SequenceEqual(form.Topics.Select(t=>t.TopicId).Order()))return "Invalid subchapter list. Reload.";
            var current=await db.Set<ProgressTopicState>().Where(p=>p.ClassSessionId==form.ClassId).ToListAsync();
            foreach(var input in form.Topics){var before=current.FirstOrDefault(p=>p.TopicId==input.TopicId)?.Percent??0;var error=ProgressRules.Transition(before,input.Percent);if(error!=null)return error;if(before<100&&input.Percent==100&&!form.ConfirmCompleted)return "Confirm that newly completed subchapters will be locked.";}
            var record=await db.Set<ProgressLessonRecord>().SingleOrDefaultAsync(r=>r.ClassSessionId==form.ClassId&&r.LessonDate==form.Date.Date&&r.ChangeKey==form.ChangeKey);
            if(record!=null&&!ProgressRules.Same(record,lesson))return "The lesson schedule or tutor changed. The previous record is retained read-only. Choose another valid lesson.";
            if(state==null){state=new(){ClassSessionId=form.ClassId,SyllabusId=s.Id};db.Add(state);}
            state.UpdatedAtUtc=DateTime.UtcNow;
            if(record==null){record=new(){ClassSessionId=form.ClassId,ClassState=state,LessonDate=lesson.Date.Date,ChangeKey=form.ChangeKey,OriginalDate=lesson.OriginalDate.Date,Start=lesson.Start,End=lesson.End,TutorUserId=Own(user),SubmittedAtUtc=DateTime.UtcNow,SubmittedLate=late};db.Add(record);}
            record.UpdatedAtUtc=DateTime.UtcNow;record.Notes=form.Notes.Trim();record.LateReason=(form.LateReason??"").Trim();bool changed=false;
            foreach(var input in form.Topics)
            {
                var value=current.FirstOrDefault(p=>p.TopicId==input.TopicId);var before=value?.Percent??0;if(before==input.Percent)continue;
                if(value==null){value=new(){ClassSessionId=form.ClassId,ClassState=state,TopicId=input.TopicId};db.Add(value);}value.Percent=input.Percent;value.CompletedAtUtc=input.Percent==100?DateTime.UtcNow:null;
                db.Add(new ProgressChange{ClassSessionId=form.ClassId,ClassState=state,LessonRecord=record,TopicId=input.TopicId,Before=before,After=input.Percent,ActorUserId=Own(user),RecordedAtUtc=DateTime.UtcNow,Note=record.Notes,LateReason=record.LateReason});changed=true;
            }
            if(!changed)db.Add(new ProgressChange{ClassSessionId=form.ClassId,ClassState=state,LessonRecord=record,ActorUserId=Own(user),RecordedAtUtc=DateTime.UtcNow,Note=record.Notes,LateReason=record.LateReason});
            await db.SaveChangesAsync();return null;
        });
    }
    public async Task<string?> ReopenAsync(ClaimsPrincipal user,ReopenProgressForm form)
    {
        if(!user.IsInRole("Admin"))return "Access denied.";
        if((form.Reason??"").Trim().Length is <10 or >500)return "Enter a correction reason of 10–500 characters.";
        return await Write(async()=>{
            if(!await Classes(user).AnyAsync(c=>c.ClassSessionId==form.ClassId))return "Class unavailable.";
            var state=await db.Set<ProgressClassState>().SingleOrDefaultAsync(s=>s.ClassSessionId==form.ClassId);
            if(state==null||!ProgressRules.Version(state.RowVersion,form.Version))return "Progress changed. Reload.";
            var item=await db.Set<ProgressTopicState>().Include(p=>p.Topic).SingleOrDefaultAsync(p=>p.ClassSessionId==form.ClassId&&p.TopicId==form.TopicId);
            if(item==null||item.Topic.SyllabusId!=state.SyllabusId)return "Subchapter unavailable.";
            if(!ProgressRules.ValidPercent(form.TargetPercent)||form.TargetPercent>75||form.TargetPercent==item.Percent||(item.Percent==100&&form.TargetPercent!=75))return "Reopen completed work at 75%, or choose a different partial progress value.";
            int before=item.Percent;item.Percent=form.TargetPercent;item.CompletedAtUtc=null;state.UpdatedAtUtc=DateTime.UtcNow;
            db.Add(new ProgressChange{ClassSessionId=form.ClassId,TopicId=form.TopicId,Before=before,After=form.TargetPercent,ActorUserId=Own(user),RecordedAtUtc=DateTime.UtcNow,Note=form.Reason!.Trim()});
            await db.SaveChangesAsync();return null;
        });
    }
    private async Task<string?> Write(Func<Task<string?>> action)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try{var error=await action();if(error!=null){await tx.RollbackAsync();db.ChangeTracker.Clear();return error;}await tx.CommitAsync();return null;}
        catch(DbUpdateConcurrencyException){await tx.RollbackAsync();db.ChangeTracker.Clear();return "This record changed. Reload before saving.";}
        catch(Exception ex)when(ex is DbUpdateException or SqlException){await tx.RollbackAsync();db.ChangeTracker.Clear();return "The change could not be saved. Reload and try again.";}
    }
}
