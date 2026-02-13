using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class ResetWalletPinRequest
    {
        [Required]
        public string Otp { get; set; } // Mã 6 số nhận được qua email

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN must be exactly 6 digits.")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "PIN must be numeric.")]
        public string NewPin { get; set; }
    }
}
