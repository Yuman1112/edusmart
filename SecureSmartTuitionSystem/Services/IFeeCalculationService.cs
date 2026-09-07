using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Services;

public class FeeBreakdown
{
    public decimal SubTotal { get; set; }
    public decimal HardcopyFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AdditionalCharge { get; set; }
    public decimal TotalAmount => SubTotal + HardcopyFee + AdditionalCharge - DiscountAmount;
    public List<(int? SubjectId, string Description, decimal Amount)> LineItems { get; set; } = new();
}

/// <summary>
/// Automated, rule-based fee calculation (Ch.2.3 "Adopted Approach and
/// Implementation", Payment &amp; Billing Module). A fixed full-month fee is
/// charged regardless of the exact start date within that month — no daily
/// pro-rating — per Ch.2.3 "Automated Fee Calculation and Invoice
/// Generation": pro-rating "would introduce a further layer of conditional
/// billing logic... for a benefit that is marginal relative to a single
/// month's fee".
/// </summary>
public interface IFeeCalculationService
{
    /// <summary>Fixed full-month fee for the given subjects, plus optional hardcopy materials charge (Functional Requirements: Payment and Billing).</summary>
    FeeBreakdown CalculateEnrollmentFee(IEnumerable<Subject> subjects, bool hardcopyOptIn, decimal hardcopyFeeAmount = 30.00m);

    /// <summary>
    /// True when <paramref name="startDate"/> falls within the final two
    /// weeks of its month, triggering the mandatory double-confirmation
    /// prompt (Functional Requirements: Class and Enrollment; Ch.2.3).
    /// </summary>
    bool IsWithinFinalTwoWeeksOfMonth(DateOnly startDate);

    /// <summary>
    /// Prorated charge for a subject added mid-cycle, based on days
    /// remaining in <paramref name="effectiveMonth"/> from
    /// <paramref name="addedOn"/> (Table 4.12 "Apply Additional Charges").
    /// </summary>
    decimal CalculateAddSubjectProration(Subject subject, DateOnly effectiveMonth, DateOnly addedOn);

    /// <summary>Generates (but does not save) the Invoice + line items for one student's regular monthly bill.</summary>
    Task<Invoice> BuildMonthlyInvoiceAsync(Student student, DateOnly billingMonth);
}
