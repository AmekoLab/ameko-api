using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderIssueRepository
    {
        Task<OrderIssue?> GetByIdAsync(Guid id, CancellationToken token = default);
        Task<IEnumerable<OrderIssue>> GetByOrderIdAsync(Guid orderId);
        Task<IEnumerable<OrderIssue>> GetByUserIdAsync(Guid userId);
        Task AddAsync(OrderIssue orderIssue);
        void Update(OrderIssue orderIssue);
        void Delete(OrderIssue orderIssue);

        Task<int> CountUserIssuesAsync(Guid userId, OrderIssueStatus status, DateTime fromDate);
        Task<List<OrderIssue>> GetExpiredIssuesAsync(DateTime threshold);
    }
}
