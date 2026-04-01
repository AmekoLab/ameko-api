using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class ShopAnalyticsRepository : IShopAnalyticsRepository
    {
        private readonly ApplicationDbContext _context;

        public ShopAnalyticsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ShopMetricsDto> GetMetricsAsync(Guid shopId, DateTime startDate, DateTime endDate)
        {
            var metrics = new ShopMetricsDto();

            // 1. Lấy tổng số đơn hàng trong kỳ
            var totalOrders = await _context.Orders
                .Where(o => o.ShopId == shopId && o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .CountAsync();
            metrics.TotalOrders = totalOrders;

            if (totalOrders == 0)
            {
                // Nếu không có đơn hàng nào trong kỳ, trả về object rỗng
                return metrics;
            }

            // 2. Tính Issue Rate
            var issueOrdersCount = await _context.OrderIssues
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.ShopId == shopId && oi.CreatedAt >= startDate && oi.CreatedAt <= endDate)
                .Select(oi => oi.OrderId)
                .Distinct()
                .CountAsync();

            metrics.IssueRate = Math.Round((double)issueOrdersCount / totalOrders * 100, 2);

            // 3. Tính Refund Rate - Tính theo số lượng đơn
            var refundedCount = await _context.Orders
                .Where(o => o.ShopId == shopId && o.OrderStatus == OrderStatus.Refunded && o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .CountAsync();

            metrics.RefundRate = Math.Round((double)refundedCount / totalOrders * 100, 2);

            // 4. Tính Repurchase Rate
            var customerGroups = await _context.Orders
                .Where(o => o.ShopId == shopId && o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .GroupBy(o => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, OrderCount = g.Count() })
                .ToListAsync();

            var totalUniqueCustomers = customerGroups.Count;
            var repeatCustomers = customerGroups.Count(c => c.OrderCount >= 2);

            if (totalUniqueCustomers > 0)
                metrics.RepurchaseRate = Math.Round((double)repeatCustomers / totalUniqueCustomers * 100, 2);

            // 5. Tính Positive Feedback Rate & Feedback Count
            var feedbacks = await _context.Feedbacks
                .Where(f => f.ShopId == shopId && f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                .Select(f => f.Rating)
                .ToListAsync();

            metrics.FeedbackCount = feedbacks.Count; // Lưu tổng số lượt đánh giá

            if (feedbacks.Any())
            {
                var positiveFeedbacks = feedbacks.Count(r => r >= 4); // 4 hoặc 5 sao
                metrics.PositiveFeedbackRate = Math.Round((double)positiveFeedbacks / feedbacks.Count * 100, 2);
            }

            // 6. Tính Auto-cancel Rate (Tỷ lệ đơn bị hủy)
            var autoCancelledCount = await _context.Orders
                .Where(o => o.ShopId == shopId &&
                            o.OrderStatus == OrderStatus.Cancelled &&
                            o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .CountAsync();

            metrics.AutoCancelRate = Math.Round((double)autoCancelledCount / totalOrders * 100, 2); // [MỚI THÊM]

            // 7. Tính Avg Response Hours
            var issuesWithResponseTimes = await _context.OrderIssues
                .Where(oi => oi.Order.ShopId == shopId && oi.CreatedAt >= startDate && oi.CreatedAt <= endDate)
                .Select(oi => new
                {
                    IssueCreateTime = oi.CreatedAt,
                    FirstReplyTime = _context.OrderIssueLogs
                        .Where(log => log.OrderIssueId == oi.Id && log.CreatedAt > oi.CreatedAt)
                        .OrderBy(log => log.CreatedAt)
                        .Select(log => (DateTime?)log.CreatedAt)
                        .FirstOrDefault()
                })
                .Where(x => x.FirstReplyTime.HasValue)
                .ToListAsync();

            if (issuesWithResponseTimes.Any())
            {
                var avgHours = issuesWithResponseTimes
                    .Average(x => (x.FirstReplyTime!.Value - x.IssueCreateTime).TotalHours);
                metrics.AvgResponseHours = Math.Round(avgHours, 2);
            }

            return metrics;
        }
    }
}