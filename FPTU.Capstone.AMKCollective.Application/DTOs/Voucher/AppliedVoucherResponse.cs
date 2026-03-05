using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class AppliedVoucherResponse
    {
        public Guid VoucherId { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public decimal DiscountApplied { get; set; }
        public int ApplyOrder { get; set; }
    }
}
