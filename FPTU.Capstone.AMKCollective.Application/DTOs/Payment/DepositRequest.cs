using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class DepositRequest
    {
        [Required]
        [Range(10000, double.MaxValue, ErrorMessage = "Minimum deposit amount is 10,000 VND")]
        public decimal Amount { get; set; }

        // Có thể thêm ReturnUrl nếu muốn redirect về trang cụ thể sau khi nạp
        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }
    }
}
