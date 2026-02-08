using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class WithdrawRequest
    {
        [Required]
        [Range(10000, double.MaxValue, ErrorMessage = "The minimum withdrawal amount is 10,000 VND")]
        public decimal Amount { get; set; }

        public string? BankName { get; set; } // Tên ngân hàng (nếu cần lưu vào Description)
        public string? BankAccountNumber { get; set; } // Số tài khoản (nếu cần lưu vào Description)
    }
}
