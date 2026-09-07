using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Filters;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.ViewModels;

namespace SecureSmartTuitionSystem.Controllers;

/// <summary>
/// Class & Enrollment Management Module — Submit/Approve/Reject/History/
/// Add-Drop (Tables 4.4-4.6, 4.12).
/// </summary>
public class EnrollmentController : Controller
{
    private readonly AppDbContext _db;
    private readonly IDuplicateRecordService _duplicateCheck;
    private readonly IFeeCalculationService _feeCalc;
    private readonly INotificationService _notifications;

    public EnrollmentController(AppDbContext db, IDuplicateRecordService duplicateCheck,
        IFeeCalculationService feeCalc, INotificationService notifications)
    {
        _db = db;
        _duplicateCheck = duplicateCheck;
        _feeCalc = feeCalc;
        _notifications = notifications;
    }

    /// <summary>
    /// Table 4.4 "Submit Enrollment Request". Allowed anonymously for a
    /// first-time applicant (Functional Requirements: "Prospective students
    /// and guardians shall be allowed to submit an initial enrollment
    /// application without logging in"); existing users submit through
    /// their authenticated account (request.IsExistingUser).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody] EnrollmentSubmitRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var session = await _db.ClassSessions.FirstOrDefaultAsync(c => c.ClassSessionId == request.ClassSessionId);
        if (session is null) return NotFound(new { message = "Class session not found." });

        // Step 5 (Check Class Capacity) - A1
        if (session.SeatsTaken >= session.MaxCapacity)
        {
            return Conflict(new { message = "Class capacity reached. Enrollment closed." });
        }

        // Mandatory double confirmation for a start date in the final two weeks of the month
        // (Functional Requirements: Class and Enrollment).
        if (_feeCalc.IsWithinFinalTwoWeeksOfMonth(request.RequestedStartDate) && !request.DoubleConfirmedLateMonthStart)
        {
            return BadRequest(new
            {
                message = "This start date falls within the final two weeks of the month; the full month's fee still applies. Please confirm to proceed.",
                requiresDoubleConfirmation = true
            });
        }

        // --- Duplicate-record validation (Ch.2.2 Adopted Approach) ---------------------
        // Last-4 comparison prioritises the IC number (matches Guardian.PhoneOrIcLast4's
        // precedence: IdentificationNo, falling back to PhoneNumber).
        var submittedLast4 = request.GuardianIdentificationNo.Length >= 4
            ? request.GuardianIdentificationNo[^4..]
            : request.GuardianIdentificationNo;

        var guardianMatch = await _duplicateCheck.CheckExistingGuardianAsync(request.GuardianEmail, submittedLast4);

        var studentDuplicate = await _duplicateCheck.CheckDuplicateStudentAsync(
            request.StudentFullName, request.StudentBirthDate);

        // Step 6 (Check Existing Enrollment) - A2: same student already active/pending for this exact class
        if (guardianMatch.IsExistingGuardianMatch && studentDuplicate.BestMatch is not null)
        {
            var alreadyEnrolled = await _db.Enrollments.AnyAsync(e =>
                e.ClassSessionId == request.ClassSessionId &&
                e.StudentId == studentDuplicate.BestMatch.StudentId &&
                (e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Pending));

            if (alreadyEnrolled)
            {
                return Conflict(new { message = "Student is already enrolled or pending approval for this class." });
            }
        }

        var enrollment = new Enrollment
        {
            ClassSessionId = request.ClassSessionId,
            GuardianId = guardianMatch.MatchedGuardianId,
            StudentId = studentDuplicate.BestMatch?.StudentId,
            RequestedStartDate = request.RequestedStartDate,
            HardcopyMaterialsOptIn = request.HardcopyMaterialsOptIn,
            DuplicateFlag = studentDuplicate.IsFlagged,
            DuplicateMatchStudentId = studentDuplicate.BestMatch?.StudentId,
            DuplicateMatchScore = studentDuplicate.BestMatch?.Score,
            Status = studentDuplicate.IsFlagged ? EnrollmentStatus.FlaggedDuplicate : EnrollmentStatus.Pending,
            Applicant = new EnrollmentApplicant
            {
                StudentFullName = request.StudentFullName,
                StudentBirthDate = request.StudentBirthDate,
                GuardianFullName = request.GuardianFullName,
                GuardianEmail = request.GuardianEmail,
                GuardianPhoneNumber = request.GuardianPhoneNumber,
                GuardianIdentificationNo = request.GuardianIdentificationNo
            }
        };

        // Multiple-subject checkbox selection (Functional Requirements: Class and Enrollment).
        var validSubjectIds = await _db.Subjects
            .Where(s => request.SubjectIds.Distinct().Contains(s.SubjectId) && s.IsActive)
            .Select(s => s.SubjectId)
            .ToListAsync();

        foreach (var subjectId in validSubjectIds)
        {
            enrollment.EnrollmentSubjects.Add(new EnrollmentSubject { SubjectId = subjectId });
        }

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        // Step 8: Notify Admin — every admin account gets one throttled notification.
        var adminIds = await _db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync();
        foreach (var adminId in adminIds)
        {
            await _notifications.SendOncePerDayAsync(adminId, NotificationType.EnrollmentApproved,
                "New enrollment request", $"A new enrollment request (#{enrollment.EnrollmentId}) is awaiting review.", enrollment.EnrollmentId);
        }

        return Ok(new
        {
            message = "Your enrollment request has been submitted for review.",
            enrollment.EnrollmentId,
            enrollment.Status
        });
    }

    /// <summary>Table 4.5 "Approve / Reject Enrollment" — Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [Audit("Enrollment.Decide", "Enrollment")]
    public async Task<IActionResult> Decide([FromBody] EnrollmentDecisionRequest request)
    {
        var enrollment = await _db.Enrollments
            .Include(e => e.Applicant)
            .Include(e => e.ClassSession).ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(e => e.EnrollmentId == request.EnrollmentId);

        if (enrollment is null) return NotFound();

        var adminUserId = this.CurrentUserId();

        if (request.Approve)
        {
            // Resolve/confirm the Guardian & Student rows now that a human has cleared any duplicate flag.
            if (enrollment.GuardianId is null)
            {
                var guardian = new Guardian
                {
                    FullName = enrollment.Applicant!.GuardianFullName,
                    Email = enrollment.Applicant.GuardianEmail,
                    PhoneNumber = enrollment.Applicant.GuardianPhoneNumber,
                    IdentificationNo = enrollment.Applicant.GuardianIdentificationNo
                };
                _db.Guardians.Add(guardian);
                await _db.SaveChangesAsync();
                enrollment.GuardianId = guardian.GuardianId;
            }

            if (enrollment.StudentId is null)
            {
                var student = new Student
                {
                    GuardianId = enrollment.GuardianId!.Value,
                    FullName = enrollment.Applicant!.StudentFullName,
                    BirthDate = enrollment.Applicant.StudentBirthDate,
                    FormYear = enrollment.ClassSession.Subject?.FormYear ?? string.Empty
                };
                _db.Students.Add(student);
                await _db.SaveChangesAsync();
                enrollment.StudentId = student.StudentId;
            }

            enrollment.Status = EnrollmentStatus.Approved;
            enrollment.DecidedAt = DateTime.UtcNow;
            enrollment.DecidedByUserId = adminUserId;
            await _db.SaveChangesAsync();

            // Fee calculation happens directly off the class's subjects — no Guardian_ID/Student_ID
            // required until now (Ch.2.4 integration point). A secure, single-use PaymentLink
            // is what actually gates account activation (Functional Requirements).
            var invoice = await BuildEnrollmentInvoiceAsync(enrollment);
            var link = new PaymentLink
            {
                InvoiceId = invoice.InvoiceId,
                Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ExpiresAt = DateTime.UtcNow.AddDays(3)
            };
            _db.PaymentLinks.Add(link);
            await _db.SaveChangesAsync();

            // Notification is sent to the applicant's e-mail out-of-band (SMTP integration point);
            // here we also log an in-system notification once the account exists.

            return Ok(new
            {
                message = "Enrollment successfully approved.",
                invoice.InvoiceId,
                invoice.TotalAmount,
                PaymentLinkToken = link.Token
            });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
            {
                return BadRequest(new { message = "A rejection reason is required." });
            }

            enrollment.Status = EnrollmentStatus.Rejected;
            enrollment.RejectionReason = request.RejectionReason;
            enrollment.DecidedAt = DateTime.UtcNow;
            enrollment.DecidedByUserId = adminUserId;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Enrollment request rejected.", request.RejectionReason });
        }
    }

    private async Task<Invoice> BuildEnrollmentInvoiceAsync(Enrollment enrollment)
    {
        var subjectIds = await _db.EnrollmentSubjects
            .Where(es => es.EnrollmentId == enrollment.EnrollmentId)
            .Select(es => es.SubjectId)
            .ToListAsync();

        // The class's own subject always applies even if EnrollmentSubjects wasn't separately populated.
        if (!subjectIds.Contains(enrollment.ClassSession.SubjectId))
            subjectIds.Add(enrollment.ClassSession.SubjectId);

        var subjects = await _db.Subjects.Where(s => subjectIds.Contains(s.SubjectId)).ToListAsync();
        var breakdown = _feeCalc.CalculateEnrollmentFee(subjects, enrollment.HardcopyMaterialsOptIn);

        var billingMonth = new DateOnly(enrollment.RequestedStartDate.Year, enrollment.RequestedStartDate.Month, 1);

        var invoice = new Invoice
        {
            StudentId = enrollment.StudentId!.Value,
            EnrollmentId = enrollment.EnrollmentId,
            BillingMonth = billingMonth,
            InvoiceType = InvoiceType.Enrollment,
            SubTotal = breakdown.SubTotal,
            HardcopyFee = breakdown.HardcopyFee,
            TotalAmount = breakdown.TotalAmount,
            Status = InvoiceStatus.PendingPayment,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            LineItems = breakdown.LineItems
                .Select(li => new InvoiceLineItem { SubjectId = li.SubjectId, Description = li.Description, Amount = li.Amount })
                .ToList()
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    /// <summary>Table 4.6 "View Enrollment History" — Student/Guardian see only their own; Admin sees all.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> History(int? classSessionId, EnrollmentStatus? status)
    {
        var role = this.CurrentUserRole();
        var userId = this.CurrentUserId();

        var query = _db.Enrollments
            .Include(e => e.ClassSession).ThenInclude(c => c.Subject)
            .AsQueryable();

        if (role != "Admin")
        {
            query = query.Where(e =>
                (e.Student != null && e.Student.UserId == userId) ||
                (e.Guardian != null && e.Guardian.UserId == userId));
        }

        if (classSessionId is not null) query = query.Where(e => e.ClassSessionId == classSessionId); // A2
        if (status is not null) query = query.Where(e => e.Status == status);                          // A3

        var results = await query
            .OrderByDescending(e => e.SubmittedAt)
            .Select(e => new
            {
                e.EnrollmentId,
                Subject = e.ClassSession.Subject.SubjectName,
                e.Status,
                e.SubmittedAt,
                e.DecidedAt
            })
            .ToListAsync();

        // A1/A4: empty list -> caller renders "No enrollment history records found." / "No historical records match your filter criteria."
        return Json(results);
    }

    /// <summary>Table 4.12 "Add/Drop Subject Billing Adjustment".</summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Audit("Enrollment.SubjectChange", "SubjectChangeRequest")]
    public async Task<IActionResult> RequestSubjectChange([FromBody] SubjectChangeRequestModel request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var subject = await _db.Subjects.FindAsync(request.SubjectId);
        if (subject is null) return NotFound(new { message = "No subject change records found for billing adjustment." });

        if (!request.DoubleConfirmed)
        {
            return BadRequest(new { message = "Please confirm the subject change before it is applied.", requiresDoubleConfirmation = true });
        }

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        // Functional Requirements: "dropping a subject only affects billing for the following month".
        // Adds may still hit the current month, prorated (Table 4.12).
        var effectiveMonth = request.IsAdd
            ? new DateOnly(now.Year, now.Month, 1)
            : new DateOnly(now.Year, now.Month, 1).AddMonths(1);

        var change = new SubjectChangeRequest
        {
            StudentId = request.StudentId,
            SubjectId = request.SubjectId,
            ChangeType = request.IsAdd ? SubjectChangeType.Add : SubjectChangeType.Drop,
            EffectiveMonth = effectiveMonth,
            RequestedByUserId = this.CurrentUserId()!,
            DoubleConfirmed = true,
            Status = SubjectChangeStatus.Applied
        };

        _db.SubjectChangeRequests.Add(change);
        await _db.SaveChangesAsync();

        if (request.IsAdd)
        {
            var proratedAmount = _feeCalc.CalculateAddSubjectProration(subject, effectiveMonth, now);

            var invoice = await _db.Invoices.FirstOrDefaultAsync(i =>
                i.StudentId == request.StudentId &&
                i.BillingMonth == effectiveMonth &&
                i.Status == InvoiceStatus.PendingPayment);

            if (invoice is null)
            {
                invoice = new Invoice
                {
                    StudentId = request.StudentId,
                    BillingMonth = effectiveMonth,
                    InvoiceType = InvoiceType.AddDropAdjustment,
                    DueDate = effectiveMonth.AddDays(7),
                    Status = InvoiceStatus.PendingPayment
                };
                _db.Invoices.Add(invoice);
            }

            invoice.LineItems.Add(new InvoiceLineItem
            {
                SubjectId = subject.SubjectId,
                Description = $"{subject.SubjectName} - Added Subject (prorated)",
                Amount = proratedAmount
            });
            invoice.AdditionalCharge += proratedAmount;
            invoice.TotalAmount = invoice.SubTotal + invoice.HardcopyFee + invoice.AdditionalCharge - invoice.DiscountAmount;
            await _db.SaveChangesAsync();

            var student = await _db.Students.FindAsync(request.StudentId);
            if (student?.UserId is not null)
            {
                await _notifications.SendOncePerDayAsync(student.UserId, NotificationType.SubjectAddCharge,
                    "Subject added", $"{subject.SubjectName} was added; RM{proratedAmount:F2} has been charged to your bill.", invoice.InvoiceId);
            }
        }
        else
        {
            // Drop: per the functional requirement, only the FOLLOWING month's bill is affected —
            // no mid-cycle refund is issued for the current month. BuildMonthlyInvoiceAsync already
            // excludes subjects with an Applied Drop for that billing month.
            var student = await _db.Students.FindAsync(request.StudentId);
            if (student?.UserId is not null)
            {
                await _notifications.SendOncePerDayAsync(student.UserId, NotificationType.SubjectDropCredit,
                    "Subject dropped", $"{subject.SubjectName} will be removed from your bill starting {effectiveMonth:MMMM yyyy}.", null);
            }
        }

        return Ok(new { message = "Subject change applied.", change.SubjectChangeRequestId, effectiveMonth });
    }
}
