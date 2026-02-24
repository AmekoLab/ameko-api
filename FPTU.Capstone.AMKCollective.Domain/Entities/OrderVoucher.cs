using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Junction table: Lưu danh sách voucher được áp dụng cho một đơn hàng (hỗ trợ stacking).
    /// </summary>
    public class OrderVoucher : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid VoucherId { get; set; }

        /// <summary>Mã voucher — lưu snapshot để tránh mất dữ liệu khi voucher bị xóa.</summary>
        public string VoucherCode { get; set; } = string.Empty;

        /// <summary>Loại voucher tại thời điểm áp dụng (snapshot).</summary>
        public VoucherType VoucherType { get; set; }

        /// <summary>Số tiền thực tế được giảm bởi voucher này sau khi tính toán.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountApplied { get; set; }

        /// <summary>Thứ tự áp dụng voucher trong stack (1 = áp dụng trước).</summary>
        public int ApplyOrder { get; set; }

        // Navigation Properties
        public virtual Order Order { get; set; } = null!;
        public virtual Voucher Voucher { get; set; } = null!;
    }
}
