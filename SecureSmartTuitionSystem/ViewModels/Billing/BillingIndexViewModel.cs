using SecureSmartTuitionSystem.Models.Billing;

namespace SecureSmartTuitionSystem.ViewModels.Billing
{
    public class BillingIndexViewModel
    {
        public int TotalBillCount { get; set; }

        public int PendingBillCount { get; set; }

        public int PaidBillCount { get; set; }

        public int OverdueBillCount { get; set; }

        public decimal TotalPendingAmount { get; set; }

        public BillStatus? SelectedStatus { get; set; }

        public string SortBy { get; set; } = "issuedAt";

        public string SortDirection { get; set; } = "desc";

        public List<BillingListItemViewModel> Bills { get; set; }
            = new();
    }

    public class BillingListItemViewModel
    {
        public int BillId { get; set; }

        public string BillNumber { get; set; } = string.Empty;

        public string ApplicationNumber { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public string GuardianName { get; set; } = string.Empty;

        public string GuardianEmail { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = "MYR";

        public BillStatus Status { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime DueAt { get; set; }

        public DateTime? PaidAt { get; set; }
    }
}