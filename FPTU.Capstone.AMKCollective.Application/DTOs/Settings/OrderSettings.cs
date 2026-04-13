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
        public decimal CustomerCancellationPenaltyRate { get; set; } = 0.1m;
        public int ShopResponseTimeoutHours { get; set; }
        public int MaxInProgressOrdersPerShopCustomer { get; set; } = 5;
        public decimal ShopPayoutRate { get; set; } = 0.8m;
        public decimal SystemVoucherPlatformShareRate { get; set; } = 0.7m;
        public decimal SystemVoucherShopShareRate { get; set; } = 0.3m;
        public decimal SystemVoucherShopShareCap { get; set; } = 50000m;
        public int ShopAssemblyInitTimeoutHours { get; set; } = 48;
        public int CustomerResponseSlaHours { get; set; } = 24;
        public int CustomerResponseReminderMaxCount { get; set; } = 3;
    }
}
