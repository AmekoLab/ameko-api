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
        public decimal Amount { get; set; }
        public decimal FeeAmount { get; set; }
        public string Currency { get; set; }
        public string Type { get; set; }   // Enum converted to string
        public string Status { get; set; } // Enum converted to string
        public string? Description { get; set; }
        public string? ShopName { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
