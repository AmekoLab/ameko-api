using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;

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

            var stats       = await _unitOfWork.Orders.GetOrderStatsForDashboardAsync(fromUtc, toUtc);
            var newUsers    = await _unitOfWork.Users.CountNewUsersAsync(fromUtc, toUtc);
            var activeShops = await _unitOfWork.Shops.CountActiveShopsAsync();

            return new AdminDashboardOverviewResponse
            {
                FromUtc               = fromUtc,
                ToUtc                 = toUtc,
                TotalOrders           = stats.TotalOrders,
                CompletedOrders       = stats.CompletedOrders,
                CancelledOrders       = stats.CancelledOrders,
                RefundedOrders        = stats.RefundedOrders,
                GrossMerchandiseValue = stats.GrossMerchandiseValue,
                NetRevenue            = stats.NetRevenue,
                PlatformRevenue       = stats.PlatformRevenue,
                ShopRevenue           = stats.ShopRevenue,
                ActiveBuyers          = stats.ActiveBuyers,
                ActiveShops           = activeShops,
                NewUsers              = newUsers,
                OrderCompletionRate   = Rate(stats.CompletedOrders, stats.TotalOrders),
            };
        }

        public async Task<AdminPaymentsHealthResponse> GetPaymentsHealthAsync(AdminDashboardFilterRequest filter)
        {
            var (fromUtc, toUtc) = ResolveDateRange(filter);

            var stats = await _unitOfWork.Payments.GetPaymentStatsForDashboardAsync(fromUtc, toUtc);

            return new AdminPaymentsHealthResponse
            {
                FromUtc                 = fromUtc,
                ToUtc                   = toUtc,
                TotalPayments           = stats.TotalPayments,
                SuccessfulPayments      = stats.SuccessfulPayments,
                FailedPayments          = stats.FailedPayments,
                RefundedPayments        = stats.RefundedPayments,
                SuccessfulPaymentVolume = stats.SuccessfulVolume,
                PaymentSuccessRate      = Rate(stats.SuccessfulPayments, stats.TotalPayments),
                MethodMetrics           = stats.MethodMetrics,
                TypeMetrics             = stats.TypeMetrics,
            };
        }

        public async Task<AdminRiskOverviewResponse> GetRiskOverviewAsync(AdminDashboardFilterRequest filter)
        {
            var (fromUtc, toUtc) = ResolveDateRange(filter);

            var orders = await _unitOfWork.Orders.GetOrderStatsForDashboardAsync(fromUtc, toUtc);
            var issues = await _unitOfWork.OrderIssues.GetIssueStatsForDashboardAsync(fromUtc, toUtc);

            return new AdminRiskOverviewResponse
            {
                FromUtc         = fromUtc,
                ToUtc           = toUtc,
                TotalOrders     = orders.TotalOrders,
                TotalIssues     = issues.TotalIssues,
                OpenIssues      = issues.OpenIssues,
                CancelRequests  = issues.CancelRequests,
                RefundRequests  = issues.RefundRequests,
                DisputeRequests = issues.DisputeRequests,
                CancelledOrders = orders.CancelledOrders,
                RefundedOrders  = orders.RefundedOrders,
                CancelRate      = Rate(orders.CancelledOrders, orders.TotalOrders),
                RefundRate      = Rate(orders.RefundedOrders, orders.TotalOrders),
                IssueRate       = Rate(issues.TotalIssues, orders.TotalOrders),
            };
        }

        public async Task<AdminTopShopsByOrdersResponse> GetTopShopsByOrdersAsync(AdminDashboardFilterRequest filter, int top = 3)
        {
            if (top <= 0)
            {
                throw new ArgumentException("Top must be greater than 0.");
            }

            var (fromUtc, toUtc) = ResolveDateRange(filter);
            var items = await _unitOfWork.Orders.GetTopShopsByOrderCountAsync(fromUtc, toUtc, top);

            return new AdminTopShopsByOrdersResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Items = items
            };
        }

        private static (DateTime fromUtc, DateTime toUtc) ResolveDateRange(AdminDashboardFilterRequest filter)
        {
            var nowUtc  = DateTime.UtcNow;
            var fromUtc = filter.StartDate?.ToUniversalTime() ?? new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc   = filter.EndDate?.ToUniversalTime() ?? nowUtc;

            if (fromUtc > toUtc)
                throw new ArgumentException("StartDate must be earlier than or equal to EndDate.");

            return (fromUtc, toUtc);
        }

        private static decimal Rate(int numerator, int denominator)
        {
            if (denominator <= 0) return 0m;
            return Math.Round((decimal)numerator * 100m / denominator, 2);
        }
    }
}
