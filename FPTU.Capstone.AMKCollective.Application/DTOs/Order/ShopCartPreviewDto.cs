using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class ShopCartPreviewDto
    {
        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }              // Tiền hàng (chỉ tính những món được tick)
        public decimal ShippingFee { get; set; }           // Phí ship của shop
        public decimal ShopDiscountAmount { get; set; }    // Tiền giảm riêng từ mã của Shop này
        public decimal TotalAmount { get; set; }           // = (SubTotal + Ship) - ShopDiscount

        public List<Guid> IncludedOrderItemIds { get; set; } = new List<Guid>();

        /// <summary>
        /// Chi tiết giảm giá từng voucher shop: mã nào giảm bao nhiêu.
        /// </summary>
        public List<VoucherDiscountBreakdown> AppliedVoucherBreakdowns { get; set; } = new List<VoucherDiscountBreakdown>();

        public string? ShopVoucherError { get; set; }
    }
}
