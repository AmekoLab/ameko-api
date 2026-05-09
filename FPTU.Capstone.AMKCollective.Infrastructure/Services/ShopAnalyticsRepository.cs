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

            // 3. Tính Refund Rate
            // - OrderStatus.Refunded: warranty claim [OrderLevel] được hoàn tiền trực tiếp
            // - OrderIssue.ReturnRequest + Completed: hàng trả về đã xử lý xong (hoàn ví), order status không đổi sang Returned
            var directRefundOrderIds = await _context.Orders
                .Where(o => o.ShopId == shopId && o.OrderStatus == OrderStatus.Refunded &&
                            o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .Select(o => o.Id)
                .ToListAsync();

            var returnRefundOrderIds = await _context.OrderIssues
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.ShopId == shopId &&
                             oi.Type == OrderIssueType.ReturnRequest &&
                             oi.Status == OrderIssueStatus.Completed &&
                             oi.CreatedAt >= startDate && oi.CreatedAt <= endDate)
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToListAsync();

            var totalRefundedOrders = directRefundOrderIds.Union(returnRefundOrderIds).Count();
            metrics.RefundRate = Math.Round((double)totalRefundedOrders / totalOrders * 100, 2);

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

            // 7. Tính Avg Response Hours — tách 2 query để tránh correlated subquery không translate được
            var issueData = await _context.OrderIssues
                .Where(oi => oi.Order.ShopId == shopId && oi.CreatedAt >= startDate && oi.CreatedAt <= endDate)
                .Select(oi => new { oi.Id, oi.CreatedAt })
                .ToListAsync();

            if (issueData.Any())
            {
                var issueIdList = issueData.Select(i => i.Id).ToList();

                var shopResponses = await _context.OrderIssueLogs
                    .Where(log => issueIdList.Contains(log.OrderIssueId) &&
                                  (log.Action == OrderIssueAction.ShopApprove ||
                                   log.Action == OrderIssueAction.ShopReject ||
                                   log.Action == OrderIssueAction.ShopReceivedReturn))
                    .GroupBy(log => log.OrderIssueId)
                    .Select(g => new { OrderIssueId = g.Key, FirstResponseAt = g.Min(l => l.CreatedAt) })
                    .ToListAsync();

                if (shopResponses.Any())
                {
                    var avgHours = issueData
                        .Join(shopResponses, i => i.Id, r => r.OrderIssueId,
                              (i, r) => (r.FirstResponseAt - i.CreatedAt).TotalHours)
                        .Average();
                    metrics.AvgResponseHours = Math.Round(avgHours, 2);
                }
            }

            return metrics;
        }
    }
}