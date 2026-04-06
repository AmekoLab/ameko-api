using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    /// <summary>
    /// Response DTO cho lịch sử rút tiền — dùng cho cả Shop (my/withdrawals)
    /// và Admin (admin/withdrawals/pending, admin/withdrawals/processed).
    /// Lấy data từ bảng WithdrawalRequest, có đầy đủ Status và thông tin xử lý.
    /// </summary>
    public class WithdrawalSummaryResponse
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }          // Số tiền gốc muốn rút
        public decimal FeeAmount { get; set; }        // Phí rút tiền (tính theo config)
        public decimal TotalDeducted { get; set; }    // Tổng bị trừ = Amount + FeeAmount

        public string BankName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankAccountName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;   // Pending / Completed / Rejected
        public string? AdminMessage { get; set; }
        public string? EvidenceUrl { get; set; }

        // Thông tin Shop (dùng cho Admin view)
        public string? ShopName { get; set; }
        public Guid UserId { get; set; }

        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
