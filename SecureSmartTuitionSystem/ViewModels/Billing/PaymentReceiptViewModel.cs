namespace SecureSmartTuitionSystem.ViewModels.Billing
{
    public sealed class PaymentReceiptViewModel
    {
        public int PaymentId { get; set; }
        public string DownloadFileName { get; set; } = "Receipt - PAID";
        public string BackController { get; set; } = "Home";
        public string BackAction { get; set; } = "Index";
        public int? BackId { get; set; }

        public string PaymentNumber { get; set; }
            = string.Empty;

        public string BillNumber { get; set; }
            = string.Empty;

        public string ApplicationNumber { get; set; }
            = string.Empty;

        public string StudentName { get; set; }
            = string.Empty;

        public string TuitionPeriod { get; set; }
            = string.Empty;

        public string GuardianName { get; set; }
            = string.Empty;

        public string GuardianEmail { get; set; }
            = string.Empty;

        public string PaymentProvider { get; set; }
            = string.Empty;

        public string Currency { get; set; }
            = "MYR";

        public decimal AmountPaid { get; set; }

        public DateTime CompletedAt { get; set; }

        public string? ProviderPaymentIntentId
        {
            get;
            set;
        }

        public decimal TuitionSubtotal { get; set; }

        public decimal TuitionDiscountAmount { get; set; }

        public decimal HardcopyFee { get; set; }

        public decimal DeliveryFee { get; set; }

        public decimal TotalAmount { get; set; }

        public List<PaymentReceiptItemViewModel> Items
        {
            get;
            set;
        } = new();
    }

    public sealed class PaymentReceiptItemViewModel
    {
        public string Description { get; set; }
            = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}