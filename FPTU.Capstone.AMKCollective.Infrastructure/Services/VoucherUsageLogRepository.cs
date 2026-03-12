using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class VoucherUsageLogRepository : IVoucherUsageLogRepository
    {
        private readonly ApplicationDbContext _context;

        public VoucherUsageLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(VoucherUsageLog log)
        {
            await _context.VoucherUsageLogs.AddAsync(log);
        }
    }
}
