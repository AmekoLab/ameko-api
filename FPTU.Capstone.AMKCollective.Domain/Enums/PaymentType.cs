using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum PaymentType
    {
        OrderPayment = 0, // Thanh toán đơn hàng

        Refund = 1, //Hoàn tiền cho khách

        Withdrawal = 2, //Rút tiền về ngân hàng

        PlatformFee = 3 //Phí sàn/Hoa hồng
    }
}
