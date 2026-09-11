using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Billing
{
    public class BillDetailsViewModel
    {
        public int BillId { get; set; }

        public string BillNumber { get; set; } = string.Empty;

        public string ApplicationNumber { get; set; } = string.Empty;

        public int EnrollmentApplicationId { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public string GuardianName { get; set; } = string.Empty;

        public string GuardianEmail { get; set; } = string.Empty;

        public string GuardianPhoneNumber { get; set; }
            = string.Empty;

        public string AcademicLevel { get; set; } = string.Empty;

        public int EnrollmentDurationMonths { get; set; }

        public EnrollmentStatus EnrollmentStatus { get; set; }

        public decimal MonthlyTuitionFee { get; set; }

        public decimal TuitionSubtotal { get; set; }

        public decimal TuitionDiscountRate { get; set; }

        public decimal TuitionDiscountAmount { get; set; }

        public decimal DiscountedTuitionAmount { get; set; }

        public int HardcopySubjectCount { get; set; }

        public decimal HardcopyFee { get; set; }

        public decimal DeliveryFee { get; set; }

        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = "MYR";

        public BillStatus Status { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime DueAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public string? CancellationReason { get; set; }

        public bool HardcopyMaterialsRequested { get; set; }

        public string? DeliveryRecipientName { get; set; }

        public string? DeliveryAddressLine1 { get; set; }

        public string? DeliveryAddressLine2 { get; set; }

        public string? DeliveryPostcode { get; set; }

        public string? DeliveryCity { get; set; }

        public string? DeliveryState { get; set; }

        public string? DeliveryRegion { get; set; }

        public List<BillItemViewModel> Items { get; set; }
            = new();

        public List<BillPaymentViewModel> Payments { get; set; }
            = new();
    }

    public class BillItemViewModel
    {
        public BillItemType ItemType { get; set; }

        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitAmount { get; set; }

        public decimal LineTotal { get; set; }
    }

    public class BillPaymentViewModel
    {
        public string PaymentNumber { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "MYR";

        public string PaymentProvider { get; set; } = string.Empty;

        public PaymentStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string? FailureReason { get; set; }
    }
}