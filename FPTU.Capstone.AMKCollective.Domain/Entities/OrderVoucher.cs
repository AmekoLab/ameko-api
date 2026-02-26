using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Junction table: Stores list of vouchers applied to an order (supports stacking).
    /// </summary>
    public class OrderVoucher : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid VoucherId { get; set; }

        /// <summary>Voucher code - snapshot to prevent data loss if voucher is deleted.</summary>
        public string VoucherCode { get; set; } = string.Empty;

        /// <summary>Voucher type at the time of application (snapshot).</summary>
        public VoucherType VoucherType { get; set; }

        /// <summary>Actual discount amount applied by this voucher after calculation.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountApplied { get; set; }

        /// <summary>Order of application in the stack (1 = applied first).</summary>
        public int ApplyOrder { get; set; }

        // Navigation Properties
        public virtual Order Order { get; set; } = null!;
        public virtual Voucher Voucher { get; set; } = null!;
    }
}
