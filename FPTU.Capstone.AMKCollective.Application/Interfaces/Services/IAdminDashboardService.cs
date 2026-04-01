using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardOverviewResponse> GetOverviewAsync(AdminDashboardFilterRequest filter);
        Task<AdminPaymentsHealthResponse> GetPaymentsHealthAsync(AdminDashboardFilterRequest filter);
        Task<AdminRiskOverviewResponse> GetRiskOverviewAsync(AdminDashboardFilterRequest filter);
    }
}
