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
        // --- Nhóm Thanh toán & Nạp rút (External Money) ---
        OrderPayment = 0,       // Khách thanh toán đơn hàng (Stripe/Bank)
        Refund = 1,             // Hoàn tiền qua cổng thanh toán (Stripe) - Ít dùng vì ta dùng Voucher
        Withdrawal = 2,         // Rút tiền về tài khoản ngân hàng
        PlatformFee = 3,        // Phí sàn/Phí rút tiền

        // --- Nhóm Vận hành Ví (Internal Money - Thay thế WalletTransaction) ---
        RefundToWallet = 4,     // Hoàn tiền vào ví (Case đặc biệt: Shop bị Ban, trả tiền mặt cho User)
        SalesPending = 5,       // Tiền bán hàng cộng vào ví Shop (Ở trạng thái Held/Giữ 1 tháng)
        SalesReleased = 6,      // Tiền bán hàng được thả (Chuyển từ Held -> Balance khả dụng)
        PaymentByWallet = 7     // Thanh toán đơn hàng bằng số dư Ví
    }
}
