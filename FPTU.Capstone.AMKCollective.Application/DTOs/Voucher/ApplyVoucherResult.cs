namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    /// <summary>
    /// Kết quả trả về khi áp dụng thành công 1 voucher vào đơn hàng.
    /// </summary>
    public class ApplyVoucherResult
    {
        /// <summary>Số tiền được giảm bởi voucher vừa áp dụng.</summary>
        public decimal NewDiscountAmount { get; set; }

        /// <summary>Tổng tiền đã giảm từ tất cả voucher trong stack.</summary>
        public decimal TotalDiscountAmount { get; set; }

        /// <summary>Tổng tiền phải thanh toán sau stacking.</summary>
        public decimal FinalTotal { get; set; }

        /// <summary>Số lượng voucher đang được áp dụng cho đơn hàng này.</summary>
        public int AppliedVouchersCount { get; set; }

        /// <summary>Chi tiết từng voucher trong stack.</summary>
        public List<AppliedVoucherDetail> AppliedVouchers { get; set; } = new();
    }

    public class AppliedVoucherDetail
    {
        public string Code { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public decimal DiscountApplied { get; set; }
        public int ApplyOrder { get; set; }
    }
}
