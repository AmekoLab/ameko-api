using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
using System;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IReputationService
    {
        Task<int> AdjustReputationAsync(ReputationTargetType targetType, Guid targetId, int delta, string? reason = null);
        Task<int> AdjustCustomerScoreAsync(Guid userId, int delta, string? reason = null);
        Task<int> AdjustShopScoreAsync(Guid shopId, int delta, string? reason = null);
        CustomerReputationGate GetCustomerGate(int score);
        ShopReputationGate GetShopGate(int score);
    }
}
