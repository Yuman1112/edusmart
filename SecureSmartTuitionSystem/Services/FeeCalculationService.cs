using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class FeeCalculationService : IFeeCalculationService
{
    private readonly AppDbContext _db;

    public FeeCalculationService(AppDbContext db)
    {
        _db = db;
    }

    public FeeBreakdown CalculateEnrollmentFee(IEnumerable<Subject> subjects, bool hardcopyOptIn, decimal hardcopyFeeAmount = 30.00m)
    {
        var breakdown = new FeeBreakdown();

        foreach (var subject in subjects)
        {
            breakdown.SubTotal += subject.MonthlyFee;
            breakdown.LineItems.Add((subject.SubjectId, $"{subject.SubjectName} - Monthly Fee", subject.MonthlyFee));
        }

        if (hardcopyOptIn)
        {
            breakdown.HardcopyFee = hardcopyFeeAmount;
            breakdown.LineItems.Add((null, "Hardcopy Learning Materials Fee", hardcopyFeeAmount));
        }

        return breakdown;
    }

    public bool IsWithinFinalTwoWeeksOfMonth(DateOnly startDate)
    {
        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        var daysRemainingIncludingToday = daysInMonth - startDate.Day + 1;
        return daysRemainingIncludingToday <= 14;
    }

    public decimal CalculateAddSubjectProration(Subject subject, DateOnly effectiveMonth, DateOnly addedOn)
    {
        // If the add takes effect in a future month, no proration applies - full month fee.
        if (effectiveMonth.Year != addedOn.Year || effectiveMonth.Month != addedOn.Month)
        {
            return subject.MonthlyFee;
        }

        var daysInMonth = DateTime.DaysInMonth(effectiveMonth.Year, effectiveMonth.Month);
        var remainingDays = daysInMonth - addedOn.Day + 1;
        var dailyRate = subject.MonthlyFee / daysInMonth;

        return Math.Round(dailyRate * remainingDays, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<Invoice> BuildMonthlyInvoiceAsync(Student student, DateOnly billingMonth)
    {
        // Active subjects for this student = subjects from Approved enrollments,
        // net of any Drop change applied for this billing month (Functional
        // Requirements: "dropping a subject only affects billing for the
        // following month, and does not affect the current month's bill").
        var enrolledSubjectIds = await _db.EnrollmentSubjects
            .Where(es => es.Enrollment.StudentId == student.StudentId
                         && es.Enrollment.Status == EnrollmentStatus.Approved)
            .Select(es => es.SubjectId)
            .Distinct()
            .ToListAsync();

        var droppedThisMonth = await _db.SubjectChangeRequests
            .Where(c => c.StudentId == student.StudentId
                        && c.ChangeType == SubjectChangeType.Drop
                        && c.Status == SubjectChangeStatus.Applied
                        && c.EffectiveMonth == billingMonth)
            .Select(c => c.SubjectId)
            .ToListAsync();

        var addedThisMonth = await _db.SubjectChangeRequests
            .Where(c => c.StudentId == student.StudentId
                        && c.ChangeType == SubjectChangeType.Add
                        && c.Status == SubjectChangeStatus.Applied
                        && c.EffectiveMonth == billingMonth)
            .Select(c => c.SubjectId)
            .ToListAsync();

        var billableSubjectIds = enrolledSubjectIds
            .Except(droppedThisMonth)
            .Union(addedThisMonth)
            .Distinct()
            .ToList();

        var subjects = await _db.Subjects
            .Where(s => billableSubjectIds.Contains(s.SubjectId))
            .ToListAsync();

        var breakdown = CalculateEnrollmentFee(subjects, hardcopyOptIn: false);

        // Due immediately on the billing date - the "1-to-7-day payment grace period"
        // described in the report is measured FROM this due date (see
        // BillingSchedulerService.ApplyOverdueRulesAsync's day-4-7 / beyond-day-7 rules),
        // not added on top of it.
        var dueDate = billingMonth;

        var invoice = new Invoice
        {
            StudentId = student.StudentId,
            BillingMonth = billingMonth,
            InvoiceType = InvoiceType.Monthly,
            SubTotal = breakdown.SubTotal,
            HardcopyFee = breakdown.HardcopyFee,
            DiscountAmount = breakdown.DiscountAmount,
            AdditionalCharge = breakdown.AdditionalCharge,
            TotalAmount = breakdown.TotalAmount,
            Status = InvoiceStatus.PendingPayment,
            DueDate = dueDate,
            LineItems = breakdown.LineItems
                .Select(li => new InvoiceLineItem { SubjectId = li.SubjectId, Description = li.Description, Amount = li.Amount })
                .ToList()
        };

        return invoice;
    }
}
