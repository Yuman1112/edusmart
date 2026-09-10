using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureSmartTuitionSystem.Models.Billing
{
    public class BillItem
    {
        public int BillItemId { get; set; }

        public int BillId { get; set; }

        public BillItemType ItemType { get; set; }

        [Required]
        [StringLength(250)]
        public string Description { get; set; }
            = string.Empty;

        [Range(1, 100000)]
        public int Quantity { get; set; } = 1;

        /*
         * Tuition discount can use a negative value.
         */
        [Column(TypeName = "decimal(10,2)")]
        [Range(
            typeof(decimal),
            "-99999999.99",
            "99999999.99")]
        public decimal UnitAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(
            typeof(decimal),
            "-99999999.99",
            "99999999.99")]
        public decimal LineTotal { get; set; }

        public Bill Bill { get; set; } = null!;
    }
}