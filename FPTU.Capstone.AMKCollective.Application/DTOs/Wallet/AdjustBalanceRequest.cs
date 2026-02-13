using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class AdjustBalanceRequest
    {
        [Required]
        public Guid UserId { get; set; } // ID của User cần điều chỉnh tiền

        [Required]
        public decimal Amount { get; set; } // Số tiền điều chỉnh (Dương = Cộng, Âm = Trừ)

        [Required]
        public string Reason { get; set; } = string.Empty; // Lý do (Bắt buộc để Audit)
    }
}
