using System;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Helpers
{
    public static class ShopRevenueCalculator
    {
        public static decimal CalculateShopRevenue(Order order, decimal payoutRate, decimal systemVoucherShopShareRate, decimal systemVoucherShopShareCap)
        {
            if (order == null) return 0m;

            decimal baseRevenue = order.SubTotal * payoutRate;
            baseRevenue -= order.DiscountAmount;

            if (order.SystemDiscountAmount > 0)
            {
                decimal shopShare = order.SystemDiscountAmount * systemVoucherShopShareRate;
                if (shopShare > systemVoucherShopShareCap) shopShare = systemVoucherShopShareCap;
                baseRevenue -= shopShare;
            }

            return Math.Max(0, baseRevenue + order.ShippingFee);
        }
    }
}
