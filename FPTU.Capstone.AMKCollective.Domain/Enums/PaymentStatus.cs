using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum PaymentStatus
    {
        Pending = 0, // Vừa tạo, chờ thanh toán

        Paid = 1, // Đã thanh toán thành công

        Failed = 2, // Thanh toán thất bại

        Refunded = 3, // Đã được hoàn tiền
        Released = 4 //giải ngân



    }
}
