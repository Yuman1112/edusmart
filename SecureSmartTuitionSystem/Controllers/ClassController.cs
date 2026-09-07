using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Filters;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Controllers;

/// <summary>
/// Class & Enrollment Management Module — class browsing, creation, and
/// schedule management (Tables 4.1-4.3). Enrollment submission/approval
/// lives in <see cref="EnrollmentController"/>.
/// </summary>
[Authorize]
public class ClassController : Controller
{
    private readonly AppDbContext _db;

    public ClassController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Table 4.3 "Browse Available Classes" — any authenticated role.</summary>
    [HttpGet]
    public async Task<IActionResult> Browse(int? subjectId, byte? dayOfWeek)
    {
        var query = _db.ClassSessions
            .Include(c => c.Subject)
            .Include(c => c.Tutor)
            .Where(c => c.Status == ClassSessionStatus.Active)
            .AsQueryable();

        if (subjectId is not null)
            query = query.Where(c => c.SubjectId == subjectId); // A2 "Filter by Subject"

        if (dayOfWeek is not null)
            query = query.Where(c => c.DayOfWeek == dayOfWeek); // A3 "Filter by Schedule"

        var classes = await query.ToListAsync();

        // A1/A4: empty results are simply an empty list here; the view/API caller
        // renders "No available classes found." / "No classes match your selected filter criteria."
        return Json(classes.Select(c => new
        {
            c.ClassSessionId,
            Subject = c.Subject.SubjectName,
            Tutor = c.Tutor?.FullName,
            c.DayOfWeek,
            c.StartTime,
            c.EndTime,
            c.Venue,
            SeatsAvailable = c.MaxCapacity - c.SeatsTaken,
            MonthlyFee = c.Subject.MonthlyFee
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int classSessionId)
    {
        var session = await _db.ClassSessions
            .Include(c => c.Subject)
            .Include(c => c.Tutor)
            .FirstOrDefaultAsync(c => c.ClassSessionId == classSessionId);

        if (session is null) return NotFound();

        return Json(session);
    }

    /// <summary>Table 4.1 "Create Class Session" — Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [Audit("Class.CreateSession", "ClassSession")]
    public async Task<IActionResult> CreateSession(int subjectId, int? tutorId, byte dayOfWeek,
        TimeOnly startTime, TimeOnly endTime, string venue, int minCapacity, int maxCapacity)
    {
        if (startTime >= endTime || string.IsNullOrWhiteSpace(venue) || minCapacity < 1 || maxCapacity < minCapacity)
        {
            // A1 - Step 4: "Invalid class details provided."
            return BadRequest(new { message = "Invalid class details provided." });
        }

        if (tutorId is not null)
        {
            var conflict = await _db.ClassSessions.AnyAsync(c =>
                c.TutorId == tutorId &&
                c.DayOfWeek == dayOfWeek &&
                c.Status == ClassSessionStatus.Active &&
                c.StartTime < endTime && startTime < c.EndTime);

            if (conflict)
            {
                // A2 - Step 6: "Selected tutor is unavailable during this timeframe."
                return Conflict(new { message = "Selected tutor is unavailable during this timeframe." });
            }
        }

        var session = new ClassSession
        {
            SubjectId = subjectId,
            TutorId = tutorId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            Venue = venue,
            MinCapacity = minCapacity,
            MaxCapacity = maxCapacity,
            Status = ClassSessionStatus.Active
        };

        _db.ClassSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Class session created successfully.", session.ClassSessionId });
    }

    /// <summary>Table 4.2 "Manage Class Schedule" (Update) — Admin/Tutor.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Tutor")]
    [ValidateAntiForgeryToken]
    [Audit("Class.UpdateSchedule", "ClassSession")]
    public async Task<IActionResult> UpdateSchedule(int classSessionId, TimeOnly startTime, TimeOnly endTime, string venue)
    {
        var session = await _db.ClassSessions.FindAsync(classSessionId);
        if (session is null) return NotFound();

        session.StartTime = startTime;
        session.EndTime = endTime;
        session.Venue = venue;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Class schedule updated successfully." });
    }

    /// <summary>Table 4.2 "Manage Class Schedule" (Cancel) — Admin/Tutor.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Tutor")]
    [ValidateAntiForgeryToken]
    [Audit("Class.CancelSession", "ClassSession")]
    public async Task<IActionResult> CancelSession(int classSessionId, string reason)
    {
        var session = await _db.ClassSessions
            .Include(c => c.Enrollments).ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(c => c.ClassSessionId == classSessionId);

        if (session is null) return NotFound();

        session.Status = ClassSessionStatus.Cancelled;
        session.CancellationReason = reason;
        await _db.SaveChangesAsync();

        // "System automatically sends notification alerts to all affected users" — see NotificationController/BackgroundServices for delivery.
        return Ok(new { message = "Class session cancelled.", affectedEnrollments = session.Enrollments.Count });
    }

    /// <summary>Table 4.2 "Manage Class Schedule" (Reschedule) — Admin/Tutor.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Tutor")]
    [ValidateAntiForgeryToken]
    [Audit("Class.RescheduleSession", "ClassSession")]
    public async Task<IActionResult> RescheduleSession(int classSessionId, byte newDayOfWeek, TimeOnly newStartTime, TimeOnly newEndTime, string newVenue)
    {
        var session = await _db.ClassSessions.FindAsync(classSessionId);
        if (session is null) return NotFound();

        var conflict = await _db.ClassSessions.AnyAsync(c =>
            c.ClassSessionId != classSessionId &&
            c.TutorId == session.TutorId &&
            c.DayOfWeek == newDayOfWeek &&
            c.Status == ClassSessionStatus.Active &&
            c.StartTime < newEndTime && newStartTime < c.EndTime);

        if (conflict)
        {
            // A4: "Time slot conflict detected."
            return Conflict(new { message = "Time slot conflict detected." });
        }

        session.DayOfWeek = newDayOfWeek;
        session.StartTime = newStartTime;
        session.EndTime = newEndTime;
        session.Venue = newVenue;
        session.Status = ClassSessionStatus.Rescheduled;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Class session rescheduled." });
    }
}
