using SecureSmartTuitionSystem.Models.Billing;
namespace SecureSmartTuitionSystem.ViewModels.Billing;
public sealed class MonthlyBillingIndexViewModel
{
    public List<MonthlyInvoice> Rows { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalRows { get; set; }
    public string? Month { get; set; }
    public string? Status { get; set; }
    public decimal Outstanding { get; set; }
}

public sealed class MonthlyEmailStatus
{
    public string Recipient { get; set; } = "";
    public DateTime Day { get; set; }
    public string Status { get; set; } = "";
    public string? Note { get; set; }
}
