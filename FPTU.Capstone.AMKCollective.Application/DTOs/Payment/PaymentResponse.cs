using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class PaymentResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; } // Để biết giao dịch của ai
        public string UserName { get; set; } = string.Empty; // Tên người dùng
        public string? ShopName { get; set; } // Tên shop (nếu có)

        public decimal Amount { get; set; }
        public decimal FeeAmount { get; set; } // Phí giao dịch (quan trọng với Admin)
        public string Currency { get; set; } = "VND";

        public string Type { get; set; } = string.Empty; // Withdrawal/Deposit...
        public string Status { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;

        public string? Description { get; set; } // Mô tả lỗi/lý do
        public DateTime CreatedAt { get; set; }
    }
}
