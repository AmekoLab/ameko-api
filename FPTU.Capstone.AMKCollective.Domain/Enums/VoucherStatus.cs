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
        Active, //Có thể sử dụng

        Expired, //Hết hạn

        Depleted, //Hết lượt dùng

        Disabled //Vô hiệu hóa
    }
}
