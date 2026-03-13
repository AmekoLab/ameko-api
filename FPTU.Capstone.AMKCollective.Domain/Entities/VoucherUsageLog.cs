using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class VoucherUsageLog : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid VoucherId { get; set; }

        /// <summary>Order that consumed this voucher (snapshot at checkout time).</summary>
        public Guid OrderId { get; set; }

        public string Code { get; set; } = string.Empty;
        public VoucherType VoucherType { get; set; }

        /// <summary>Actual discount amount applied by this voucher on this order.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountApplied { get; set; }
        public int ApplyOrder { get; set; }

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual Voucher Voucher { get; set; } = null!;
        public virtual Order Order { get; set; } = null!;
    }
}
