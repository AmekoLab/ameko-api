using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminDashboardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<AdminDashboardOverviewResponse> GetOverviewAsync(AdminDashboardFilterRequest filter)
        {
            var (fromUtc, toUtc) = ResolveDateRange(filter);

            var orders = await _unitOfWork.Orders.GetOrdersForDashboardAsync(fromUtc, toUtc);
            var users = await _unitOfWork.Users.GetUsersForDashboardAsync(fromUtc, toUtc);
            var (_, activeShopsCount) = await _unitOfWork.Shops.GetShopsAsync(null, ShopStatus.Active, 1, 1);

            var businessOrders = orders.Where(o => o.OrderStatus != OrderStatus.InCart).ToList();
            var paidOrders = businessOrders.Where(o => o.PaymentStatus == PaymentStatus.Paid).ToList();

            var totalOrders = businessOrders.Count;
            var completedOrders = businessOrders.Count(o => o.OrderStatus == OrderStatus.Completed);
            var cancelledOrders = businessOrders.Count(o => o.OrderStatus == OrderStatus.Cancelled);
            var refundedOrders = businessOrders.Count(o => o.OrderStatus == OrderStatus.Refunded || o.PaymentStatus == PaymentStatus.Refunded);

            var gmv = businessOrders.Sum(o => o.TotalAmount);
            var netRevenue = paidOrders
                .Where(o => o.OrderStatus != OrderStatus.Cancelled && o.OrderStatus != OrderStatus.Refunded)
                .Sum(o => o.TotalAmount);

            return new AdminDashboardOverviewResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                RefundedOrders = refundedOrders,
                GrossMerchandiseValue = gmv,
                NetRevenue = netRevenue,
                ActiveBuyers = businessOrders.Select(o => o.CustomerId).Distinct().Count(),
                ActiveShops = activeShopsCount,
                NewUsers = users.Count,
                OrderCompletionRate = Rate(completedOrders, totalOrders)
            };
        }

        public async Task<AdminPaymentsHealthResponse> GetPaymentsHealthAsync(AdminDashboardFilterRequest filter)
        {
            var (fromUtc, toUtc) = ResolveDateRange(filter);
            var payments = await _unitOfWork.Payments.GetPaymentsForDashboardAsync(fromUtc, toUtc);

            var totalPayments = payments.Count;
            var successfulPayments = payments.Count(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Released);
            var failedPayments = payments.Count(p => p.Status == PaymentStatus.Failed);
            var refundedPayments = payments.Count(p => p.Status == PaymentStatus.Refunded);

            var methodMetrics = payments
                .GroupBy(p => p.Method)
                .Select(g => new PaymentMethodMetric
                {
                    Method = g.Key,
                    Total = g.Count(),
                    Successful = g.Count(x => x.Status == PaymentStatus.Paid || x.Status == PaymentStatus.Released),
                    Failed = g.Count(x => x.Status == PaymentStatus.Failed)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var typeMetrics = payments
                .GroupBy(p => p.Type)
                .Select(g => new PaymentTypeMetric
                {
                    Type = g.Key,
                    Total = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            return new AdminPaymentsHealthResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalPayments = totalPayments,
                SuccessfulPayments = successfulPayments,
                FailedPayments = failedPayments,
                RefundedPayments = refundedPayments,
                SuccessfulPaymentVolume = payments
                    .Where(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Released)
                    .Sum(p => p.Amount),
                PaymentSuccessRate = Rate(successfulPayments, totalPayments),
                MethodMetrics = methodMetrics,
                TypeMetrics = typeMetrics
            };
        }

        public async Task<AdminRiskOverviewResponse> GetRiskOverviewAsync(AdminDashboardFilterRequest filter)
        {
            var (fromUtc, toUtc) = ResolveDateRange(filter);
            var orders = await _unitOfWork.Orders.GetOrdersForDashboardAsync(fromUtc, toUtc);
            var issues = await _unitOfWork.OrderIssues.GetIssuesForDashboardAsync(fromUtc, toUtc);

            var businessOrders = orders.Where(o => o.OrderStatus != OrderStatus.InCart).ToList();
            var totalOrders = businessOrders.Count;

            var cancelRequests = issues.Count(i => i.Type == OrderIssueType.CancelRequest);
            var refundRequests = issues.Count(i => i.Type == OrderIssueType.ReturnRequest);
            var disputeRequests = issues.Count(i => i.Type == OrderIssueType.ReturnRequest || i.Type == OrderIssueType.WarrantyClaim);

            var openIssues = issues.Count(i =>
                i.Status == OrderIssueStatus.Pending ||
                i.Status == OrderIssueStatus.InProgress ||
                i.Status == OrderIssueStatus.ShopAccepted ||
                i.Status == OrderIssueStatus.AwaitingReturn ||
                i.Status == OrderIssueStatus.Returning);

            var cancelledOrders = businessOrders.Count(o => o.OrderStatus == OrderStatus.Cancelled);
            var refundedOrders = businessOrders.Count(o => o.OrderStatus == OrderStatus.Refunded || o.OrderStatus == OrderStatus.Returned);

            return new AdminRiskOverviewResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalOrders = totalOrders,
                TotalIssues = issues.Count,
                OpenIssues = openIssues,
                CancelRequests = cancelRequests,
                RefundRequests = refundRequests,
                DisputeRequests = disputeRequests,
                CancelledOrders = cancelledOrders,
                RefundedOrders = refundedOrders,
                CancelRate = Rate(cancelledOrders, totalOrders),
                RefundRate = Rate(refundedOrders, totalOrders),
                IssueRate = Rate(issues.Count, totalOrders)
            };
        }

        private static (DateTime fromUtc, DateTime toUtc) ResolveDateRange(AdminDashboardFilterRequest filter)
        {
            var nowUtc = DateTime.UtcNow;
            var fromUtc = filter.StartDate?.ToUniversalTime() ?? new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = filter.EndDate?.ToUniversalTime() ?? nowUtc;

            if (fromUtc > toUtc)
            {
                throw new ArgumentException("StartDate must be earlier than or equal to EndDate.");
            }

            return (fromUtc, toUtc);
        }

        private static decimal Rate(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                return 0m;
            }

            return Math.Round((decimal)numerator * 100m / denominator, 2);
        }
    }
}
