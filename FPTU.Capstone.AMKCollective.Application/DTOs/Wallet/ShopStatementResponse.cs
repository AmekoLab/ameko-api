using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    /// <summary>
    /// Báo cáo tổng hợp tài chính theo tháng cho shop (sales / withdrawals / fees).
    /// </summary>
    public class ShopStatementResponse
    {
        public int Month { get; set; }
        public int Year { get; set; }

        /// <summary>Số dư khả dụng (Balance) đầu kỳ — trước transaction đầu tiên của kỳ.</summary>
        public decimal OpeningBalance { get; set; }

        /// <summary>Số dư khả dụng (Balance) cuối kỳ — sau transaction cuối cùng của kỳ.</summary>
        public decimal ClosingBalance { get; set; }

        /// <summary>Số dư đang giữ (HeldBalance) đầu kỳ.</summary>
        public decimal OpeningHeldBalance { get; set; }

        /// <summary>Số dư đang giữ (HeldBalance) cuối kỳ.</summary>
        public decimal ClosingHeldBalance { get; set; }

        /// <summary>Tổng doanh thu đã ghi nhận (SalesRevenue) — phần net shop nhận được, đã trừ phí platform.</summary>
        public decimal TotalSalesRevenue { get; set; }

        /// <summary>Tổng doanh thu đang chờ (SalesPending) phát sinh trong kỳ — chưa release.</summary>
        public decimal TotalSalesPending { get; set; }

        /// <summary>Tổng tiền hoàn cho khách bị trừ vào shop (ManualAdjustment / OrderRefund out-flow).</summary>
        public decimal TotalRefundsDeducted { get; set; }

        /// <summary>Tổng tiền rút thành công về ngân hàng trong kỳ (Withdrawal status=Completed).</summary>
        public decimal TotalWithdrawals { get; set; }

        /// <summary>Tổng tiền rút đang Pending tính tới cuối kỳ.</summary>
        public decimal TotalPendingWithdrawals { get; set; }

        /// <summary>Tổng phí platform đã trừ trên các giao dịch SalesRevenue trong kỳ.</summary>
        public decimal TotalPlatformFees { get; set; }

        /// <summary>Số transaction trong kỳ.</summary>
        public int TransactionCount { get; set; }

        /// <summary>Danh sách transaction trong kỳ (đã sort theo CreatedAt asc).</summary>
        public List<WalletTransactionResponse> Transactions { get; set; } = new();
    }
}
