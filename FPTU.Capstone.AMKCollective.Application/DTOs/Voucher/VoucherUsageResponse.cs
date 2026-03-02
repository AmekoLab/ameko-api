using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class VoucherUsageResponse
    {
        public Guid OrderId { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal OrderTotalAmount { get; set; }
        public decimal DiscountApplied { get; set; }
        public DateTime AppliedAt { get; set; } 
    }
}
