using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class WalletTransactionResponse
    {
        public Guid Id { get; set; }
        
        // Gửi nguyên si lượng tiền tuyệt đối để UI tự format
        public decimal Amount { get; set; } 
        
        // Trả về luồng giao dịch: "In" (+), "Out" (-), "Held" (Đang giữ)
        public string FlowDirection { get; set; } 
        
        public decimal BalanceAfterTransaction { get; set; } // để minh bạch
        public decimal HeldBalanceAfterTransaction { get; set; } // để minh bạch cho shop
        
        // Tự động tính toán số dư trước giao dịch
        public decimal BalanceBeforeTransaction => FlowDirection == "In" ? BalanceAfterTransaction - Math.Abs(Amount) : 
                                                   FlowDirection == "Out" ? BalanceAfterTransaction + Math.Abs(Amount) : 
                                                   BalanceAfterTransaction;
        public decimal FeeAmount { get; set; }
        public string Currency { get; set; }
        public string Type { get; set; }   // Enum converted to string
        public string Status { get; set; } // Enum converted to string
        public string? Description { get; set; }
        public string? ShopName { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }

        public Guid? RelatedOrderId { get; set; }
        public Guid? OrderGroupId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
