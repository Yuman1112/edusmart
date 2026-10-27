namespace SecureSmartTuitionSystem.ViewModels.Billing;
public sealed class BillingCoverageViewModel
{
    public string Month { get; set; } = "";
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public List<BillingCoverageRow> Rows { get; set; } = new();
}
public sealed class BillingCoverageRow
{
    public string Student { get; set; } = "";
    public string StudentNumber { get; set; } = "";
    public string Class { get; set; } = "";
    public string ApplicationNumber { get; set; } = "";
    public DateTime StartMonth { get; set; }
    public DateTime EndMonth { get; set; }
    public string EnrollmentStatus { get; set; } = "";
    public DateTime? DropEffectiveDate { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? BillNumber { get; set; }
    public string BillStatus { get; set; } = "";
    public bool NeedsReview { get; set; }
}
