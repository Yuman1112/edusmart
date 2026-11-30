using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;
using SecureSmartTuitionSystem.Services.Tutor;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Admin,Tutor")]
public sealed class ClassSchedulesController(ApplicationDbContext db, ClassScheduleService schedules) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<ClassSession> Allowed => User.IsInRole("Admin") ? db.ClassSessions : db.ClassSessions.Where(x => x.TutorUserId == UserId);
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date, string mode = "month", int? classSessionId = null)
    {
        if (classSessionId.HasValue && !await Allowed.AnyAsync(x => x.ClassSessionId == classSessionId)) return NotFound();
        var anchor = date?.Date ?? ClassScheduleService.Today;
        if (anchor.Year < 2000 || anchor.Year > 2100) return BadRequest();
        mode = mode is "today" or "week" or "month" ? mode : "month";
        var first = new DateTime(anchor.Year, anchor.Month, 1);
        var from = mode == "today" ? anchor : mode == "week" ? anchor.AddDays(-((int)anchor.DayOfWeek + 6) % 7) : first;
        var to = mode == "today" ? from : mode == "week" ? from.AddDays(6) : first.AddMonths(1).AddDays(-1);
        var model = new CalendarModel { Anchor = anchor, From = from, To = to, Mode = mode, ClassSessionId = classSessionId,
            Lessons = await schedules.GetLessonsAsync(from, to, User.IsInRole("Admin") ? null : UserId, classSessionId) };
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Cancel(int classSessionId, DateTime date)
    {
        var c = await Allowed.AsNoTracking().FirstOrDefaultAsync(x => x.ClassSessionId == classSessionId);
        if (c == null) return NotFound();
        if (!LateMonthEnrollmentPolicy.IsRegularTeachingDate(date) || !ClassScheduleService.ValidOn(c, date) || date.DayOfWeek != c.DayOfWeek || date.Date.Add(c.EndTime.ToTimeSpan()) <= ClassScheduleService.Now) return BadRequest();
        ViewBag.ClassName = c.ClassName ?? "Class";
        return View(new CancelLessonModel { ClassSessionId = classSessionId, Date = date.Date });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancelLessonModel model)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var c = await Allowed.FirstOrDefaultAsync(x => x.ClassSessionId == model.ClassSessionId);
        if (c == null) return NotFound();
        ViewBag.ClassName = c.ClassName ?? "Class";
        model.Date = model.Date.Date;
        model.Reason = model.Reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError(nameof(model.Reason), "Please enter a cancellation reason.");
        if (!LateMonthEnrollmentPolicy.IsRegularTeachingDate(model.Date) || !ClassScheduleService.ValidOn(c, model.Date) || model.Date.DayOfWeek != c.DayOfWeek || model.Date.Add(c.EndTime.ToTimeSpan()) <= ClassScheduleService.Now)
            ModelState.AddModelError("", "Only a scheduled lesson that has not finished can be cancelled.");
        if (await db.ClassLessonChanges.AnyAsync(x => x.ClassSessionId == c.ClassSessionId && x.OriginalDate == model.Date))
            ModelState.AddModelError("", "This lesson has already been cancelled. Use Pending Replacements.");
        if (!ModelState.IsValid) return View(model);
        var students = await schedules.RosterAsync(c.ClassSessionId, model.Date);
        var recipients = await schedules.RecipientsAsync(students, c.TutorUserId);
        db.ClassLessonChanges.Add(new ClassLessonChange { ClassSessionId = c.ClassSessionId, OriginalDate = model.Date,
            OriginalStartTime = c.StartTime, OriginalEndTime = c.EndTime, CancellationReason = model.Reason.Trim(),
            CancelledAtUtc = DateTime.UtcNow, ChangedByUserId = UserId,
            StudentIdsJson = JsonSerializer.Serialize(students), RecipientIdsJson = JsonSerializer.Serialize(recipients) });
        await schedules.NotifyAsync(recipients, "Class cancelled", $"{c.ClassName}: {model.Date:dd MMM yyyy} {c.StartTime:HH:mm} cancelled. {model.Reason.Trim()} Replacement date is pending.", c.ClassSessionId);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        TempData["SuccessMessage"] = "Only this dated lesson was cancelled. It is listed in Pending Replacements.";
        return RedirectToAction(nameof(Pending));
    }
    [HttpGet]
    public async Task<IActionResult> Pending(bool includeScheduled = false)
    {
        var ids = Allowed.Select(x => x.ClassSessionId);
        var query = db.ClassLessonChanges.AsNoTracking().Include(x => x.ClassSession).Where(x => ids.Contains(x.ClassSessionId));
        ViewBag.PendingCount = await query.CountAsync(x => x.ReplacementDate == null);
        ViewBag.ScheduledCount = await query.CountAsync(x => x.ReplacementDate != null);
        query = includeScheduled ? query.Where(x => x.ReplacementDate != null) : query.Where(x => x.ReplacementDate == null);
        ViewBag.IncludeScheduled = includeScheduled;
        return View(await query.OrderBy(x => x.OriginalDate).ThenBy(x => x.ClassSession.ClassCode).ToListAsync());
    }
    [HttpGet]
    public async Task<IActionResult> Replacement(int id)
    {
        var c = await LoadChange(id);
        if (c == null) return NotFound();
        if (c.ClassSession.Status == ClassSessionStatus.Closed) return BadRequest();
        if (c.ReplacementDate.HasValue && c.ReplacementDate.Value.Add(c.ReplacementStartTime!.Value.ToTimeSpan()) <= ClassScheduleService.Now) return BadRequest();
        return View(new ReplacementModel { ChangeId = id, Original = c, Date = c.ReplacementDate,
            Start = c.ReplacementStartTime ?? c.OriginalStartTime, End = c.ReplacementEndTime ?? c.OriginalEndTime,
            Venue = c.ReplacementVenue ?? c.ClassSession.Venue });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Replacement(ReplacementModel model)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var c = await LoadChange(model.ChangeId);
        if (c == null) return NotFound();
        model.Original = c;
        foreach (var error in await ReplacementErrors(c, model)) ModelState.AddModelError("", error);
        if (!ModelState.IsValid) return View(model);
        if (c.ReplacementDate == model.Date!.Value.Date && c.ReplacementStartTime == model.Start && c.ReplacementEndTime == model.End && c.ReplacementVenue == model.Venue.Trim())
            return RedirectToAction(nameof(Pending), new { includeScheduled = true });
        c.ReplacementDate = model.Date!.Value.Date; c.ReplacementStartTime = model.Start; c.ReplacementEndTime = model.End;
        c.ReplacementVenue = model.Venue.Trim(); c.ChangedByUserId = UserId;
        var recipients = JsonSerializer.Deserialize<List<string>>(c.RecipientIdsJson) ?? new();
        await schedules.NotifyAsync(recipients, "Replacement scheduled", $"{c.ClassSession.ClassName}: replacement for {c.OriginalDate:dd MMM yyyy} is on {c.ReplacementDate:dd MMM yyyy}, {model.Start:HH:mm}–{model.End:HH:mm} MYT. Meet: {c.ReplacementVenue}", c.ClassSessionId);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        TempData["SuccessMessage"] = "Replacement saved; affected users were notified.";
        return RedirectToAction(nameof(Index), new { date = c.ReplacementDate.Value.ToString("yyyy-MM-dd"), mode = "today" });
    }
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CheckReplacement(ReplacementModel model)
    {
        var c = await LoadChange(model.ChangeId);
        if (c == null) return NotFound();
        var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Enter a valid date and time." : x.ErrorMessage).ToList();
        errors.AddRange(await ReplacementErrors(c, model));
        return Json(new { valid = errors.Count == 0, messages = errors.Distinct().ToList() });
    }
    private async Task<List<string>> ReplacementErrors(ClassLessonChange c, ReplacementModel model)
    {
        var errors = new List<string>();
        if (c.ClassSession.Status == ClassSessionStatus.Closed) { errors.Add("A closed class is read-only."); return errors; }
        if (c.ReplacementDate.HasValue && c.ReplacementDate.Value.Add(c.ReplacementStartTime!.Value.ToTimeSpan()) <= ClassScheduleService.Now)
            errors.Add("A replacement that has started cannot be rescheduled.");
        if (!ClassScheduleService.MeetLink(model.Venue)) errors.Add("Use a valid HTTPS Google Meet link.");
        if (!model.Date.HasValue || !model.Start.HasValue || !model.End.HasValue) { errors.Add("Choose the replacement date, start time and end time."); return errors; }
        if (model.Date.Value.Year != c.OriginalDate.Year) errors.Add("Replacement must take place within the original academic year, by 31 December.");
        var duration = model.End.Value - model.Start.Value;
        if (model.Start >= model.End) errors.Add("End time must be after start time.");
        if (duration < TimeSpan.FromMinutes(30) || duration > TimeSpan.FromHours(4)) errors.Add("A lesson must last between 30 minutes and 4 hours.");
        if (model.Date.Value.Year < 2000 || model.Date.Value.Year > 2100 || model.Date.Value.Date.Add(model.Start.Value.ToTimeSpan()) <= ClassScheduleService.Now)
            errors.Add("Choose a future date and time (through 2100).");
        if (model.Date.Value.Date == c.OriginalDate && model.Start == c.OriginalStartTime && model.End == c.OriginalEndTime)
            errors.Add("Choose a different date or time from the cancelled lesson.");
        
        bool timetableChanged = c.ReplacementDate?.Date != model.Date.Value.Date
            || c.ReplacementStartTime != model.Start || c.ReplacementEndTime != model.End;
        if (errors.Count == 0 && timetableChanged)
        {
            string? teachingError = await new TutorTeachingEligibilityService(db)
                .ValidateSubjectAsync(c.ClassSession.TutorUserId, c.ClassSession.SubjectId);
            if (teachingError != null) errors.Add(teachingError);

            string? availabilityError = await new TutorAvailabilityService(db).ValidateAsync(
                c.ClassSession.TutorUserId, model.Date.Value.DayOfWeek, model.Start.Value, model.End.Value);
            if (availabilityError != null) errors.Add(availabilityError);
        }
        if (errors.Count == 0 && await schedules.HasConflictAsync(c, model.Date.Value.Date, model.Start.Value, model.End.Value))
            errors.Add("The tutor or an affected student already has a class during this time.");
        return errors;
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unschedule(int changeId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var c = await LoadChange(changeId);
        if (c == null) return NotFound();
        if (c.ClassSession.Status == ClassSessionStatus.Closed) return BadRequest();
        if (!c.ReplacementDate.HasValue) return RedirectToAction(nameof(Pending));
        if (c.ReplacementDate.Value.Add(c.ReplacementStartTime!.Value.ToTimeSpan()) <= ClassScheduleService.Now) return BadRequest();
        var oldDate = c.ReplacementDate.Value;
        c.ReplacementDate = null; c.ReplacementStartTime = null; c.ReplacementEndTime = null; c.ReplacementVenue = null; c.ChangedByUserId = UserId;
        await schedules.NotifyAsync(JsonSerializer.Deserialize<List<string>>(c.RecipientIdsJson) ?? new(), "Replacement cancelled",
            $"{c.ClassSession.ClassName}: replacement on {oldDate:dd MMM yyyy} has been cancelled. {reason.Trim()} A new replacement date is pending.", c.ClassSessionId);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        TempData["SuccessMessage"] = "Replacement cancelled; the original lesson is pending again.";
        return RedirectToAction(nameof(Pending));
    }
    private Task<ClassLessonChange?> LoadChange(int id)
    {
        var ids = Allowed.Select(x => x.ClassSessionId);
        return db.ClassLessonChanges.Include(x => x.ClassSession).FirstOrDefaultAsync(x => x.ClassLessonChangeId == id && ids.Contains(x.ClassSessionId));
    }
}
