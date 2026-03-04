using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class OrderSettings
    {
        public decimal DefaultShippingFee { get; set; }
        public int AbandonedOrderTimeoutHours { get; set; }
        public int WarrantyPeriodDays { get; set; }
        public int CancellationSpamCheckDays { get; set; }
        public int MaxCancellationsPerPeriod { get; set; }
        public decimal ShopCancellationPenaltyRate { get; set; }
        public int ShopResponseTimeoutHours { get; set; }
    }
}
