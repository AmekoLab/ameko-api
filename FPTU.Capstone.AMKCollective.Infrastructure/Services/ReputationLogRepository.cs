using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class ReputationLogRepository : IReputationLogRepository
    {
        private readonly ApplicationDbContext _context;

        public ReputationLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ReputationLog log)
        {
            await _context.ReputationLogs.AddAsync(log);
        }

        public async Task<(IEnumerable<ReputationLog> Items, int TotalCount)> GetByTargetAsync(string targetType, Guid targetId, int pageNumber, int pageSize)
        {
            var query = _context.ReputationLogs
                .Where(x => x.TargetType == targetType && x.TargetId == targetId);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
