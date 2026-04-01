using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IQualityScoreService
    {
        Task<QualityScoreResultDto> CalculateShopScoreAsync(Guid shopId, DateTime startDate, DateTime endDate);
        Task<CurrentReputationDto?> GetCurrentReputationAsync(Guid shopId);
        Task<QualityScoreSnapshot?> GetReputationBreakdownAsync(Guid shopId);
        Task<IEnumerable<ReputationTrendDto>> GetReputationTrendAsync(Guid shopId);
        Task<IEnumerable<BadgeHistoryDto>> GetBadgeHistoryAsync(Guid shopId);
    }
}
