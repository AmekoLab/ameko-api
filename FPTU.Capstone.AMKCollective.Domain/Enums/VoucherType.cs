using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum VoucherType
    {
        Promotion = 0,      // Voucher khuyến mãi thông thường (Admin/Shop tạo)
        Compensation = 1,   // Voucher đền bù/Hoàn tiền (Hệ thống tự tạo khi hủy đơn)
        Negotiation = 2     // Voucher thương lượng (Shop tạo riêng cho 1 khách)
    }
}
