using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum WalletTransactionType
    {
        Deposit, //Nạp tiền vào ví

        Withdraw, //Rút tiền về tài khoản ngân hàng

        Payment,//Thanh toán đơn hàng

        Refund, //Nhận tiền hoàn (Refund)

        SalesPending, // Tiền bán hàng (đang bị giữ)

        SalesReleased, // Tiền chuyển từ Held -> Available

        Fee // Phí sàn/Phí rút tiền
    }
}