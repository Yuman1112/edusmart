using SecureSmartTuitionSystem.Services.Tables;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Parent,Student")]
public sealed class EnrollmentHistoryController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, EnrollmentStatus? status = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        if (status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest();

        var query = db.EnrollmentApplications.AsNoTracking().Where(a =>
            a.SubmittedByUserId == userId ||
            (User.IsInRole("Parent") && (a.ExistingParentProfile != null && a.ExistingParentProfile.ApplicationUserId == userId ||
                a.ExistingStudentProfile != null && a.ExistingStudentProfile.ParentStudents.Any(link => link.IsActive && link.ParentProfile.ApplicationUserId == userId))) ||
            (User.IsInRole("Student") && a.ExistingStudentProfile != null && a.ExistingStudentProfile.ApplicationUserId == userId));
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        const int pageSize = 25;
        var display = from application in query
            from subject in application.SelectedSubjects.DefaultIfEmpty()
            select new {
                application.EnrollmentApplicationId,
                SubjectId = subject == null ? 0 : subject.EnrollmentApplicationSubjectId,
                application.ApplicationNumber, application.StudentName, application.SubmittedAt,
                application.RequestedStartDate, DurationMonths = application.EnrollmentDurationMonths,
                application.Status, application.RejectionReason,
                ClassName = subject == null ? "—" : subject.ClassSession.ClassName ?? subject.ClassSession.ClassCode
            };
        var totalRows = await display.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        var rowOffset = (page - 1) * pageSize;
        var ordered = TableOrder.Apply(display, HttpContext?.Request.Query, "submitted", true,
            ("student", x => x.StudentName), ("class", x => x.ClassName), ("submitted", x => x.SubmittedAt),
            ("start", x => x.RequestedStartDate), ("duration", x => x.DurationMonths), ("status", x => x.Status == EnrollmentStatus.ApprovedPendingPayment ? "Approved Pending Payment" :
                x.Status == EnrollmentStatus.Cancelled ? "Cancelled" : x.Status == EnrollmentStatus.Enrolled ? "Enrolled" :
                x.Status == EnrollmentStatus.PaymentConfirmed ? "Payment Confirmed" : x.Status == EnrollmentStatus.PendingReview ? "Pending Review" : "Rejected"))
            .ThenByDescending(x => x.EnrollmentApplicationId).ThenBy(x => x.SubjectId);
        var rows = await ordered.Skip(rowOffset).Take(pageSize).Select(x => new EnrollmentHistoryRow {
            ApplicationNumber = x.ApplicationNumber, StudentName = x.StudentName, SubmittedAt = x.SubmittedAt,
            RequestedStartDate = x.RequestedStartDate, DurationMonths = x.DurationMonths, Status = x.Status,
            RejectionReason = x.RejectionReason, Classes = new List<string> { x.ClassName }
        }).ToListAsync();
        return View(new EnrollmentHistoryViewModel { Rows = rows, Page = page, TotalPages = pages,
            Status = status, RowOffset = rowOffset, TotalRows = totalRows });
    }
}
