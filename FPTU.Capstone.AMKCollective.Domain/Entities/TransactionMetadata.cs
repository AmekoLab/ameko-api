using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class TransactionMetadata
    {
        public decimal? PenaltyRate { get; set; } // Phí phạt (%)
        public decimal? ShippingFee { get; set; } // Phí ship liên quan
        public decimal? SystemDiscount { get; set; } // Voucher hệ thống khấu trừ
        public string? Note { get; set; }
    }
}
