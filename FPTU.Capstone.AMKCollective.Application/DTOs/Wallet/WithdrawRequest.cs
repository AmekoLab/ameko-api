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
        [Range(100000, double.MaxValue, ErrorMessage = "The minimum withdrawal amount is 100,000 VND")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Please enter your PIN for verification.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN must be 6 digits.")]
        public string WalletPin { get; set; }
    }
}
