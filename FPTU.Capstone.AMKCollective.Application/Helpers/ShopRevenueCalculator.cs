using System;
using System.Collections.Generic;
using System.Linq;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Helpers
{
    public static class ShopRevenueCalculator
    {
        /// <summary>
        /// Tính doanh thu shop từ một tập con các items (dùng cho partial cancel).
        /// shippingFee truyền vào > 0 chỉ khi đây là lần cuối (hủy hết order).
        /// </summary>
        public static decimal CalculateItemsRevenue(
            IEnumerable<OrderItem> items,
            decimal shippingFee,
            decimal payoutRate,
            decimal systemVoucherShopShareRate,
            decimal systemVoucherShopShareCap)
        {
            var list = items.ToList();
            if (!list.Any()) return 0m;

            decimal subTotal = list.Sum(i => i.TotalPrice);
            decimal shopDiscount = list.Sum(i => i.ShopAllocatedDiscount);
            decimal sysDiscount = list.Sum(i => i.SystemAllocatedDiscount);

            decimal revenue = subTotal * payoutRate - shopDiscount;
            if (sysDiscount > 0)
            {
                decimal shopShare = sysDiscount * systemVoucherShopShareRate;
                if (shopShare > systemVoucherShopShareCap) shopShare = systemVoucherShopShareCap;
                revenue -= shopShare;
            }

            return Math.Max(0, revenue + shippingFee);
        }

        public static decimal CalculateShopRevenue(Order order, decimal payoutRate, decimal systemVoucherShopShareRate, decimal systemVoucherShopShareCap)
        {
            if (order == null) return 0m;

            // Chỉ tính active items — cancelled items không được tính vào doanh thu shop
            var activeItems = order.OrderItems.Where(i => i.ItemStatus == OrderItemStatus.Active).ToList();

            decimal effectiveSubTotal = activeItems.Any()
                ? activeItems.Sum(i => i.TotalPrice)
                : order.SubTotal;

            decimal shopVoucherDiscount = activeItems.Any()
                ? activeItems.Sum(i => i.ShopAllocatedDiscount)
                : 0;

            decimal systemVoucherDiscount = activeItems.Any()
                ? activeItems.Sum(i => i.SystemAllocatedDiscount)
                : order.SystemDiscountAmount;

            decimal baseRevenue = effectiveSubTotal * payoutRate;
            // Shop chịu toàn bộ chi phí voucher của chính họ
            baseRevenue -= shopVoucherDiscount;
            // Shop chịu một phần chi phí system voucher theo tỷ lệ cấu hình
            if (systemVoucherDiscount > 0)
            {
                decimal shopShare = systemVoucherDiscount * systemVoucherShopShareRate;
                if (shopShare > systemVoucherShopShareCap) shopShare = systemVoucherShopShareCap;
                baseRevenue -= shopShare;
            }

            return Math.Max(0, baseRevenue + order.ShippingFee);
        }
    }
}
