using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.Models.Billing
{
    public class Bill
    {
        public int BillId { get; set; }

        [Required]
        [StringLength(7)]
        [RegularExpression(
            @"^BL\d{5}$",
            ErrorMessage =
                "Bill number must follow BL00001 format.")]
        public string BillNumber { get; set; }
            = string.Empty;

        public int EnrollmentApplicationId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal TuitionSubtotal { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Range(typeof(decimal), "0", "100")]
        public decimal TuitionDiscountRate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal TuitionDiscountAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal DiscountedTuitionAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal HardcopyFee { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal DeliveryFee { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        public BillStatus Status { get; set; }
            = BillStatus.PendingPayment;

        public DateTime IssuedAt { get; set; }
            = DateTime.UtcNow;

        public DateTime DueAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        [StringLength(500)]
        public string? CancellationReason { get; set; }

        public EnrollmentApplication EnrollmentApplication
        {
            get;
            set;
        } = null!;

        public ICollection<BillItem> Items { get; set; }
            = new List<BillItem>();

        public ICollection<Payment> Payments { get; set; }
            = new List<Payment>();
    }
}