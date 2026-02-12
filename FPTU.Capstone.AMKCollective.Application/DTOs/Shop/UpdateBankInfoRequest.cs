using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class UpdateBankInfoRequest
    {
        [Required]
        public string BankName { get; set; }

        [Required]
        public string BankAccountNumber { get; set; }

        [Required]
        public string BankAccountName { get; set; } // Tên chủ tài khoản

        // --- BẢO MẬT KÉP ---
        [Required(ErrorMessage = "Password is required for authentication.")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Wallet PIN is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN must be a 6-digit number.")]
        public string WalletPin { get; set; }
    }
}
