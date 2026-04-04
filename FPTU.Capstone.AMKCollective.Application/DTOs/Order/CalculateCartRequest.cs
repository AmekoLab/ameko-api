using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class CalculateCartRequest
    {
        // 1. Danh sách ID của các món hàng (CartItem) được người dùng TICK CHỌN để thanh toán
        public List<Guid> SelectedOrderItemIds { get; set; } = new List<Guid>(); //SelectedCartItemIds

        // 2. Mã giảm giá của Hệ Thống/Sàn (Nếu khách có chọn)
        public string? AppliedSystemVoucherCode { get; set; }

        // 3. Danh sách mã giảm giá của từng Shop (Nếu khách có chọn)
        // Key: ShopId (ID của ShopProfile), Value: VoucherCode
        public Dictionary<Guid, string> AppliedShopVoucherCodes { get; set; } = new Dictionary<Guid, string>();

        // 4. Nhiều voucher riêng của từng Shop (chỉ dùng nếu cần stacking voucher tặng riêng)
        // Key: ShopId (ID của ShopProfile), Value: List of VoucherCode
        public Dictionary<Guid, List<string>>? AppliedShopVoucherCodeGroups { get; set; }
    }
}
