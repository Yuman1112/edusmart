using SecureSmartTuitionSystem.Models.Billing;

namespace SecureSmartTuitionSystem.ViewModels.Billing
{
    public class PaymentCheckoutViewModel
    {
        public string PaymentAccessToken { get; set; }
            = string.Empty;

        public bool PreserveSession { get; set; }

        public string BillNumber { get; set; }
            = string.Empty;

        public string ApplicationNumber { get; set; }
            = string.Empty;

        public string StudentName { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; }
            = string.Empty;

        public string GuardianName { get; set; }
            = string.Empty;

        public string GuardianEmail { get; set; }
            = string.Empty;

        public int EnrollmentDurationMonths { get; set; }

        public decimal TuitionSubtotal { get; set; }

        public decimal TuitionDiscountAmount { get; set; }

        public decimal HardcopyFee { get; set; }

        public decimal DeliveryFee { get; set; }

        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = "MYR";

        public BillStatus Status { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime DueAt { get; set; }

        public List<PaymentCheckoutItemViewModel>
            Items
        { get; set; } = new();
    }

    public class PaymentCheckoutItemViewModel
    {
        public string Description { get; set; }
            = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}