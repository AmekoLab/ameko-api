using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum DiscountType
    {
        Percentage = 0,     // Giảm theo phần trăm (VD: 10%)
        FixedAmount = 1     // Giảm số tiền cố định (VD: 50.000 VND) - Dùng cho Hoàn tiền
    }
}
