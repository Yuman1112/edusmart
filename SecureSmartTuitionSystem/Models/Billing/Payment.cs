using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureSmartTuitionSystem.Models.Billing
{
    public class Payment
    {
        public int PaymentId { get; set; }

        [Required]
        [StringLength(7)]
        [RegularExpression(
            @"^PY\d{5}$",
            ErrorMessage =
                "Payment number must follow PY00001 format.")]
        public string PaymentNumber { get; set; }
            = string.Empty;

        public int BillId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [Required]
        [StringLength(50)]
        public string PaymentProvider { get; set; }
            = "Stripe";

        [StringLength(255)]
        public string? ProviderCheckoutSessionId
        {
            get;
            set;
        }

        [StringLength(255)]
        public string? ProviderPaymentIntentId
        {
            get;
            set;
        }

        [StringLength(255)]
        public string? ProviderEventId
        {
            get;
            set;
        }

        public PaymentStatus Status { get; set; }
            = PaymentStatus.Pending;

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public DateTime? FailedAt { get; set; }

        [StringLength(500)]
        public string? FailureReason { get; set; }

        public Bill Bill { get; set; } = null!;
    }
}