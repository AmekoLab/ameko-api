using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum VoucherStatus
    {
        Active = 0, //Có thể sử dụng

        Expired = 1, //Hết hạn

        Depleted = 2, //Hết lượt dùng

        Disabled = 3 //Vô hiệu hóa
    }
}
