using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class WalletStatisticsResponse
    {
        public decimal AvailableBalance { get; set; }  // Số dư hiện tại
        public decimal HeldBalance { get; set; }       // Số dư đang bị giữ
        public decimal TotalRevenue { get; set; }      // Tổng thu nhập (Tiền đã thực sự về túi - SalesReleased)
        public decimal TotalWithdrawn { get; set; }    // Tổng tiền đã rút thành công
        public decimal PendingWithdrawal { get; set; } // Tổng tiền đang chờ rút
        public decimal ThisMonthRevenue { get; set; }  // Doanh thu tính trong tháng hiện tại
    }
}
