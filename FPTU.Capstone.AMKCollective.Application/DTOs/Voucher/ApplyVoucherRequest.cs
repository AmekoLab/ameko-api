using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class ApplyVoucherRequest
    {
        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public string Code { get; set; } = string.Empty;
    }
}
