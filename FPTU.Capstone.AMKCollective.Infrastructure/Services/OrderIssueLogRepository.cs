using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class OrderIssueLogRepository : IOrderIssueLogRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderIssueLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OrderIssueLog>> GetByOrderIssueIdAsync(Guid orderIssueId)
        {
            return await _context.OrderIssueLogs
                .Include(log => log.ActionBy)
                .Where(log => log.OrderIssueId == orderIssueId && !log.IsDeleted)
                .OrderBy(log => log.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(OrderIssueLog log)
        {
            await _context.OrderIssueLogs.AddAsync(log);
        }

        public void Delete(OrderIssueLog log)
        {
            log.IsDeleted = true;
            _context.OrderIssueLogs.Update(log);
        }
    }
}
