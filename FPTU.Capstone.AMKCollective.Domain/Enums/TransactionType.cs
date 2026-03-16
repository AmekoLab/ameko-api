using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum TransactionType
    {
        OrderPayment = 0,       // Trừ tiền khi thanh toán đơn hàng bằng Wallet
        OrderRefund = 1,        // Cộng tiền khi đơn hàng thất bại/hủy và được hoàn về Wallet
        SalesRevenue = 2,       // Cộng tiền doanh thu bán hàng cho Shop
        Deposit = 3,            // Cộng tiền khi user nạp tiền vào Wallet (thông qua Payment/Stripe)
        Withdrawal = 4,         // Trừ tiền khi tạo WithdrawalRequest rút tiền về ngân hàng
        SalesPending = 5,       // Tiền bị giữ
        ManualAdjustment = 6    // Điều chỉnh số dư thủ công
    }
}
