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
        // Page actual class rows, not applications. A legacy application without
        // classes is represented by one placeholder row.
        var ordered = query.OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.EnrollmentApplicationId);
        var rowCounts = await ordered.Select(a => a.SelectedSubjects.Count).ToListAsync();
        var totalRows = rowCounts.Sum(n => Math.Max(1, n));
        var pages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        var rowOffset = (page - 1) * pageSize;
        var applicationOffset = 0;
        var precedingRows = 0;
        while (applicationOffset < rowCounts.Count && precedingRows + Math.Max(1, rowCounts[applicationOffset]) <= rowOffset)
        {
            precedingRows += Math.Max(1, rowCounts[applicationOffset]);
            applicationOffset++;
        }
        // At most 25 applications are needed to fill 25 rows, since each has
        // at least one display row. The first may be partially on the prior page.
        var applications = await ordered.Skip(applicationOffset).Take(pageSize)
            .Select(a => new EnrollmentHistoryRow
            {
                ApplicationNumber = a.ApplicationNumber, StudentName = a.StudentName,
                SubmittedAt = a.SubmittedAt, RequestedStartDate = a.RequestedStartDate,
                DurationMonths = a.EnrollmentDurationMonths, Status = a.Status,
                RejectionReason = a.RejectionReason,
                Classes = a.SelectedSubjects.OrderBy(s => s.EnrollmentApplicationSubjectId)
                    .Select(s => s.ClassSession.ClassName ?? s.ClassSession.ClassCode).ToList()
            }).ToListAsync();
        var rows = applications.SelectMany(application => application.Classes.Count == 0
            ? new[] { application }
            : application.Classes.Select(className => new EnrollmentHistoryRow
            {
                ApplicationNumber = application.ApplicationNumber, StudentName = application.StudentName,
                SubmittedAt = application.SubmittedAt, RequestedStartDate = application.RequestedStartDate,
                DurationMonths = application.DurationMonths, Status = application.Status,
                RejectionReason = application.RejectionReason, Classes = new List<string> { className }
            }))
            .Skip(rowOffset - precedingRows).Take(pageSize).ToList();
        return View(new EnrollmentHistoryViewModel { Rows = rows, Page = page, TotalPages = pages,
            Status = status, RowOffset = rowOffset, TotalRows = totalRows });
    }
}
