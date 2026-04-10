namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    /// <summary>
    /// Breakdown of how much each voucher discounted within a shop preview.
    /// </summary>
    public class VoucherDiscountBreakdown
    {
        /// <summary>Mã voucher được áp dụng.</summary>
        public string VoucherCode { get; set; } = string.Empty;

        /// <summary>Loại giảm giá (e.g. "Percentage", "FixedAmount").</summary>
        public string DiscountType { get; set; } = string.Empty;

        /// <summary>Số tiền thực tế được giảm bởi voucher này.</summary>
        public decimal DiscountAmount { get; set; }
    }
}
