using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class CalculateCartResponse
    {
        // --- A. TỔNG KẾT TOÀN BỘ GIỎ HÀNG (Hiển thị ở thanh công cụ dưới cùng) ---
        public decimal TotalCartSubTotal { get; set; }     // Tổng tiền hàng của các món được tick
        public decimal TotalShippingFee { get; set; }     
        public decimal TotalDiscountAmount { get; set; }   
        public decimal FinalTotalAmount { get; set; }      
        public string? SystemVoucherError { get; set; }

        // --- B. CHI TIẾT BÁO GIÁ CHO TỪNG SHOP ---
        public List<ShopCartPreviewDto> ShopPreviews { get; set; } = new List<ShopCartPreviewDto>();
    }
}
