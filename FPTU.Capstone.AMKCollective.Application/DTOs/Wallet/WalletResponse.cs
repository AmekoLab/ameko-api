using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class WalletResponse
    {
        public Guid UserId { get; set; }
        public decimal Balance { get; set; }       // Số dư khả dụng
        public decimal HeldBalance { get; set; }   // Số dư đang bị giữ
        public string Currency { get; set; } = "VND";
        public bool IsActive { get; set; }
    }
}
