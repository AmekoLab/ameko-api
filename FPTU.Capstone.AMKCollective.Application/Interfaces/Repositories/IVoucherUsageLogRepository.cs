using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IVoucherUsageLogRepository
    {
        Task AddAsync(VoucherUsageLog log);
        void Update(VoucherUsageLog log);
        void Delete(VoucherUsageLog log);
        Task DeleteAllByOrderIdAsync(Guid orderId);

        Task<IEnumerable<VoucherUsageLog>> GetByOrderIdAsync(Guid orderId);
        Task<VoucherUsageLog?> GetByOrderAndVoucherAsync(Guid orderId, Guid voucherId);
        Task<(IEnumerable<VoucherUsageLog> Items, int TotalCount)> GetUsageByVoucherIdAsync(Guid voucherId, int pageNumber, int pageSize);
        Task<(IEnumerable<VoucherUsageLog> Items, int TotalCount)> GetAllUsagesAsync(Guid? creatorId, int pageNumber, int pageSize);
        Task<int> CountUsageByUserAndVoucherAsync(Guid userId, Guid voucherId, Guid excludeOrderId);
    }
}
