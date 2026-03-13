using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum VoucherScope
    {
        System = 0, // Voucher sàn (áp dụng mọi nơi)
        Shop = 1    // Voucher shop (chỉ áp dụng cho sản phẩm của Shop đó)
    }
}
