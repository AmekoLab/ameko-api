using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IShopDashboardService
    {
        Task<ShopCustomerBehaviorOverviewResponse> GetCustomerBehaviorOverviewAsync(Guid shopUserId, ShopBehaviorFilterRequest filter);
        Task<List<ShopCustomerTrendResponse>> GetCustomerBehaviorTrendAsync(Guid shopUserId, ShopBehaviorFilterRequest filter);
        Task<PaginatedResult<ShopTopSpenderResponse>> GetTopSpendersAsync(Guid shopUserId, ShopBehaviorFilterRequest filter);
        Task<PaginatedResult<ShopChurnRiskCustomerResponse>> GetChurnRiskCustomersAsync(Guid shopUserId, ShopBehaviorFilterRequest filter);
        Task<ShopPurchaseFrequencyResponse> GetPurchaseFrequencyAsync(Guid shopUserId, ShopBehaviorFilterRequest filter);
        Task<ShopConversionResponse> GetConversionSummaryAsync(Guid shopUserId, ShopBehaviorFilterRequest filter);
    }
}
