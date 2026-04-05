using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IReputationLogRepository
    {
        Task AddAsync(ReputationLog log);
        Task<(IEnumerable<ReputationLog> Items, int TotalCount)> GetByTargetAsync(string targetType, Guid targetId, int pageNumber, int pageSize);
    }
}
