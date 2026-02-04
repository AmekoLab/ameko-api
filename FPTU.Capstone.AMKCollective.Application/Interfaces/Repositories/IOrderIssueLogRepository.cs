using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderIssueLogRepository
    {
        Task<IEnumerable<OrderIssueLog>> GetByOrderIssueIdAsync(Guid orderIssueId);
        Task AddAsync(OrderIssueLog log);
        void Delete(OrderIssueLog log);
    }
}
