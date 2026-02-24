namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    /// <summary>
    /// Result returned when successfully applying a voucher to an order.
    /// </summary>
    public class ApplyVoucherResult
    {
        /// <summary>Discount amount from the newly applied voucher.</summary>
        public decimal NewDiscountAmount { get; set; }

        /// <summary>Total discount from all vouchers in the stack.</summary>
        public decimal TotalDiscountAmount { get; set; }

        /// <summary>Final total amount to pay after stacking.</summary>
        public decimal FinalTotal { get; set; }

        /// <summary>Number of vouchers currently applied to this order.</summary>
        public int AppliedVouchersCount { get; set; }

        /// <summary>Details of each voucher in the stack.</summary>
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
