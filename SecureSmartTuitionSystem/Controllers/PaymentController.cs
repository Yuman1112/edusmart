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
/// Payment & Billing Module (Tables 4.7-4.13). Monthly bill auto-generation
/// and overdue suspension/reactivation are handled by
/// BackgroundServices/BillingSchedulerService, not this controller.
/// </summary>
[Authorize]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFeeCalculationService _feeCalc;
    private readonly IPaymentGatewayClient _gateway;
    private readonly IEncryptionService _encryption;
    private readonly INotificationService _notifications;

    public PaymentController(AppDbContext db, IFeeCalculationService feeCalc, IPaymentGatewayClient gateway,
        IEncryptionService encryption, INotificationService notifications)
    {
        _db = db;
        _feeCalc = feeCalc;
        _gateway = gateway;
        _encryption = encryption;
        _notifications = notifications;
    }

    /// <summary>Table 4.7 "Enrollment Fee Calculation" — Admin manual recompute/override with discount or additional charge.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [Audit("Payment.CalculateFee", "Invoice")]
    public async Task<IActionResult> RecalculateFee(int invoiceId, decimal? discountAmount, decimal? additionalCharge)
    {
        var invoice = await _db.Invoices.Include(i => i.LineItems).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        if (invoice is null)
        {
            // A1 - Step 2: "Student enrollment record not found."
            return NotFound(new { message = "Student enrollment record not found." });
        }

        if (discountAmount is not null) invoice.DiscountAmount = discountAmount.Value;      // A3 "Apply Discount"
        if (additionalCharge is not null) invoice.AdditionalCharge = additionalCharge.Value; // A2 "Apply Additional Charges"

        invoice.TotalAmount = invoice.SubTotal + invoice.HardcopyFee + invoice.AdditionalCharge - invoice.DiscountAmount;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Monthly bill generated successfully.", invoice.InvoiceId, invoice.TotalAmount });
    }

    /// <summary>Table 4.8 "View Monthly Bill".</summary>
    [HttpGet]
    public async Task<IActionResult> CurrentBill(int studentId)
    {
        if (!await CanAccessStudentAsync(studentId)) return Forbid();

        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.StudentId == studentId && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.BillingMonth)
            .FirstOrDefaultAsync();

        if (invoice is null)
        {
            // A1: "No active monthly bill found."
            return NotFound(new { message = "No active monthly bill found." });
        }

        var outstandingTotal = await _db.Invoices
            .Where(i => i.StudentId == studentId && (i.Status == InvoiceStatus.PendingPayment || i.Status == InvoiceStatus.Overdue))
            .SumAsync(i => i.TotalAmount);

        return Json(new
        {
            invoice.InvoiceId,
            invoice.BillingMonth,
            invoice.DueDate,
            invoice.Status,
            invoice.TotalAmount,
            LineItems = invoice.LineItems.Select(li => new { li.Description, li.Amount }),
            OutstandingBalance = outstandingTotal
        });
    }

    /// <summary>Table 4.9 "Make Payment".</summary>
    [HttpPost]
    [AllowAnonymous] // the PaymentLinkToken path covers the pre-account, initial-enrollment payment
    [ValidateAntiForgeryToken]
    [Audit("Payment.MakePayment", "Payment")]
    public async Task<IActionResult> Pay([FromBody] MakePaymentRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        Invoice? invoice;
        PaymentLink? link = null;

        if (!string.IsNullOrEmpty(request.PaymentLinkToken))
        {
            link = await _db.PaymentLinks.Include(l => l.Invoice)
                .FirstOrDefaultAsync(l => l.Token == request.PaymentLinkToken);

            if (link is null || !link.IsValid)
            {
                return BadRequest(new { message = "This payment link is invalid or has expired." });
            }
            invoice = link.Invoice;
        }
        else if (request.InvoiceId is not null)
        {
            if (!await CanAccessInvoiceAsync(request.InvoiceId.Value)) return Forbid();
            invoice = await _db.Invoices.FindAsync(request.InvoiceId.Value);
        }
        else
        {
            return BadRequest(new { message = "Either an invoice id or a payment link token is required." });
        }

        if (invoice is null) return NotFound();
        if (invoice.Status == InvoiceStatus.Paid) return Conflict(new { message = "This bill has already been paid." });

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            AmountPaid = invoice.TotalAmount,
            PaymentMethod = request.PaymentMethod,
            GatewayTokenEncrypted = string.Empty, // set below once the gateway responds
            PayerNoteEncrypted = request.PayerNote is null ? null : _encryption.Encrypt(request.PayerNote),
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var gatewayResult = await _gateway.ChargeAsync(new GatewayChargeRequest
        {
            Amount = invoice.TotalAmount,
            PaymentMethod = request.PaymentMethod,
            RawPaymentDetails = request.RawPaymentDetails // forwarded only, never stored
        });

        if (gatewayResult.IsSuccess)
        {
            payment.Status = PaymentStatus.Success;
            payment.GatewayTokenEncrypted = _encryption.Encrypt(gatewayResult.GatewayToken!);
            payment.ProcessedAt = DateTime.UtcNow;

            invoice.Status = InvoiceStatus.Paid;

            var receipt = new Receipt
            {
                PaymentId = payment.PaymentId,
                ReceiptNumber = $"RCPT-{DateTime.UtcNow:yyyyMMdd}-{payment.PaymentId:D6}"
            };
            _db.Receipts.Add(receipt);

            if (link is not null) link.UsedAt = DateTime.UtcNow;

            // Account activation gated on payment confirmation (Functional Requirements: Class and Enrollment).
            if (invoice.InvoiceType == InvoiceType.Enrollment)
            {
                await ActivateAccountsAsync(invoice);
            }

            await _db.SaveChangesAsync();

            var student = await _db.Students.FindAsync(invoice.StudentId);
            if (student?.UserId is not null)
            {
                await _notifications.SendOncePerDayAsync(student.UserId, NotificationType.PaymentConfirmed,
                    "Payment successful", $"Your payment of RM{payment.AmountPaid:F2} was received. Receipt {receipt.ReceiptNumber}.", invoice.InvoiceId);
            }

            return Ok(new { message = "Payment successful.", receipt.ReceiptNumber, payment.PaymentId });
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = gatewayResult.FailureReason;
            payment.GatewayTokenEncrypted = _encryption.Encrypt(string.Empty);
            await _db.SaveChangesAsync();

            var student = await _db.Students.FindAsync(invoice.StudentId);
            if (student?.UserId is not null)
            {
                await _notifications.SendOncePerDayAsync(student.UserId, NotificationType.PaymentFailed,
                    "Payment failed", "Your payment could not be processed. Please try again.", invoice.InvoiceId);
            }

            return BadRequest(new { message = "Payment failed. Please try again." });
        }
    }

    /// <summary>
    /// Guardian_ID/Student_ID were already assigned at enrollment-approval
    /// time in this design (see EnrollmentController.Decide) so that
    /// duplicate-checking has a stable Student row to flag against; what
    /// payment confirmation actually gates is the linked ApplicationUser
    /// account becoming usable (Functional Requirements: "only activate a
    /// student or guardian account after the... fee has been confirmed paid").
    /// </summary>
    private async Task ActivateAccountsAsync(Invoice invoice)
    {
        var student = await _db.Students.Include(s => s.Guardian).FirstOrDefaultAsync(s => s.StudentId == invoice.StudentId);
        if (student is null) return;

        // This is the very first invoice for this family, so neither account
        // may have an ApplicationUser row yet (Guardian.UserId/Student.UserId
        // are only populated once payment is confirmed - Functional
        // Requirements: Class and Enrollment). Row creation here is a stub:
        // real credential issuance (temp password, welcome e-mail, actual
        // login username) is the User Authentication & RBAC Module's job -
        // this method's only responsibility is to establish the FK link and
        // flip IsActive so the rest of this report's modules see an active
        // account immediately.
        if (student.Guardian is not null)
        {
            if (student.Guardian.UserId is null)
            {
                var guardianUser = new ApplicationUser
                {
                    UserName = student.Guardian.Email,
                    Email = student.Guardian.Email,
                    Role = UserRole.Guardian,
                    IsActive = true
                };
                _db.Users.Add(guardianUser);
                await _db.SaveChangesAsync();
                student.Guardian.UserId = guardianUser.Id;
            }
            else
            {
                var guardianUser = await _db.Users.FindAsync(student.Guardian.UserId);
                if (guardianUser is not null) guardianUser.IsActive = true;
            }
        }

        if (student.UserId is null)
        {
            // AspNetUsers.Email is unique, so the student's placeholder row cannot
            // reuse the guardian's e-mail verbatim - a "+studentId" sub-address
            // keeps it delivering to the same inbox while staying unique.
            var atIndex = student.Guardian?.Email.IndexOf('@') ?? -1;
            var studentEmail = atIndex > 0
                ? $"{student.Guardian!.Email[..atIndex]}+student{student.StudentId}{student.Guardian.Email[atIndex..]}"
                : $"student{student.StudentId}@placeholder.local";

            var studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                Role = UserRole.Student,
                IsActive = true
            };
            _db.Users.Add(studentUser);
            await _db.SaveChangesAsync();
            student.UserId = studentUser.Id;
        }
        else
        {
            var studentUser = await _db.Users.FindAsync(student.UserId);
            if (studentUser is not null) studentUser.IsActive = true;
        }
    }

    /// <summary>Table 4.10 "View Receipt".</summary>
    [HttpGet]
    public async Task<IActionResult> Receipt(int paymentId)
    {
        var receipt = await _db.Receipts
            .Include(r => r.Payment).ThenInclude(p => p.Invoice)
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

        if (receipt is null)
        {
            // A1: "No payment receipts found."
            return NotFound(new { message = "No payment receipts found." });
        }

        if (!await CanAccessInvoiceAsync(receipt.Payment.InvoiceId)) return Forbid();

        return Json(new
        {
            receipt.ReceiptNumber,
            receipt.IssuedAt,
            receipt.Payment.AmountPaid,
            receipt.Payment.PaymentMethod,
            receipt.Payment.PaymentId
        });
    }

    /// <summary>Table 4.11 "Payment Status Tracking".</summary>
    [HttpGet]
    public async Task<IActionResult> Status(int? studentId, DateOnly? from, DateOnly? to, PaymentStatus? status)
    {
        var role = this.CurrentUserRole();
        var query = _db.Payments.Include(p => p.Invoice).AsQueryable();

        if (role != "Admin")
        {
            var userId = this.CurrentUserId();
            query = query.Where(p =>
                (p.Invoice.Student.UserId == userId) || (p.Invoice.Student.Guardian.UserId == userId));
        }
        else if (studentId is not null)
        {
            query = query.Where(p => p.Invoice.StudentId == studentId);
        }

        if (from is not null) query = query.Where(p => DateOnly.FromDateTime(p.CreatedAt) >= from); // A1
        if (to is not null) query = query.Where(p => DateOnly.FromDateTime(p.CreatedAt) <= to);      // A1
        if (status is not null) query = query.Where(p => p.Status == status);                        // A2

        var results = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.PaymentId, p.InvoiceId, p.AmountPaid, p.Status, p.CreatedAt })
            .ToListAsync();

        // A3: empty list -> "No payment status records match your selected criteria."
        return Json(results);
    }

    /// <summary>Table 4.13 "Payment History Viewing".</summary>
    [HttpGet]
    public async Task<IActionResult> History(int studentId, int? month, int? year, PaymentStatus? status)
    {
        if (!await CanAccessStudentAsync(studentId)) return Forbid();

        var query = _db.Payments.Include(p => p.Invoice).Where(p => p.Invoice.StudentId == studentId);

        if (month is not null && year is not null)
            query = query.Where(p => p.Invoice.BillingMonth.Month == month && p.Invoice.BillingMonth.Year == year); // A1

        if (status is not null) query = query.Where(p => p.Status == status); // A2

        var results = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.PaymentId, p.Invoice.BillingMonth, p.AmountPaid, p.Status })
            .ToListAsync();

        // A3: empty list -> "No historical payment records match your selected filter criteria."
        return Json(results);
    }

    private async Task<bool> CanAccessStudentAsync(int studentId)
    {
        var role = this.CurrentUserRole();
        if (role == "Admin") return true;

        var userId = this.CurrentUserId();
        return await _db.Students.AnyAsync(s =>
            s.StudentId == studentId && (s.UserId == userId || s.Guardian.UserId == userId));
    }

    private async Task<bool> CanAccessInvoiceAsync(int invoiceId)
    {
        var role = this.CurrentUserRole();
        if (role == "Admin") return true;

        var userId = this.CurrentUserId();
        return await _db.Invoices.AnyAsync(i =>
            i.InvoiceId == invoiceId && (i.Student.UserId == userId || i.Student.Guardian.UserId == userId));
    }
}
