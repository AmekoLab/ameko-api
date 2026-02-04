using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderIssueRepository
    {
        Task<OrderIssue?> GetByIdAsync(Guid id);
        Task<IEnumerable<OrderIssue>> GetByOrderIdAsync(Guid orderId);
        Task<IEnumerable<OrderIssue>> GetByUserIdAsync(Guid userId);
        Task AddAsync(OrderIssue orderIssue);
        void Update(OrderIssue orderIssue);
        void Delete(OrderIssue orderIssue);
    }
}
