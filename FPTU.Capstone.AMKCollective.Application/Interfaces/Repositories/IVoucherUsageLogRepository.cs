using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IVoucherUsageLogRepository
    {
        Task AddAsync(VoucherUsageLog log);
    }
}
