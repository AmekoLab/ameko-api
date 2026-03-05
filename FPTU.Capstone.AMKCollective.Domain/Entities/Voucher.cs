using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Voucher : BaseEntity
    {
        public Guid? CreatorId { get; set; }
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty; //voucher's name
        public string? Description { get; set; }

        public VoucherType Type { get; set; } = VoucherType.Promotion;
        public DiscountType DiscountType { get; set; } = DiscountType.FixedAmount;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; } // e.g., 10% or 50,000 VND

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; } // Maximum discount amount (for Percentage type)

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinOrderValue { get; set; } = 0; // Minimum order value required

        // --- Time configuration & usage limits ---
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int UsageLimit { get; set; } = 1; // Total number of times the voucher can be used
        public int UsedCount { get; set; } = 0;  // Number of times already used

        public VoucherStatus Status { get; set; } = VoucherStatus.Active;
        public Guid? TargetUserId { get; set; }

        // --- Stacking Configuration ---
        /// <summary>Indicates whether this voucher can be combined with other vouchers.</summary>
        public bool IsStackable { get; set; } = false;

        /// <summary>Stacking policy: None, WithCompensationOnly, or All.</summary>
        public StackingPolicy StackingPolicy { get; set; } = StackingPolicy.None;

        // Navigation Properties
        public virtual User? Creator { get; set; } = null!;
        public virtual User? TargetUser { get; set; }
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<VoucherUsageLog> VoucherUsageLogs { get; set; } = new List<VoucherUsageLog>();
        public virtual ICollection<OrderVoucher> OrderVouchers { get; set; } = new List<OrderVoucher>();
    }
}
