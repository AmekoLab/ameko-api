using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class OrderIssueRepository : IOrderIssueRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderIssueRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<OrderIssue?> GetByIdAsync(Guid id, CancellationToken token = default)
        {
            return await _context.OrderIssues
                .Include(oi => oi.Logs)
                .Include(oi => oi.Order)
                .Include(oi => oi.User)
                .FirstOrDefaultAsync(oi => oi.Id == id && !oi.IsDeleted, token);
        }

        public async Task<IEnumerable<OrderIssue>> GetByOrderIdAsync(Guid orderId)
        {
            return await _context.OrderIssues
                .Include(oi => oi.Logs)
                .Where(oi => oi.OrderId == orderId && !oi.IsDeleted)
                .ToListAsync();
        }

        public async Task<IEnumerable<OrderIssue>> GetByUserIdAsync(Guid userId)
        {
            return await _context.OrderIssues
                .Include(oi => oi.Logs)
                .Include(oi => oi.Order)
                .Where(oi => oi.UserId == userId && !oi.IsDeleted)
                .ToListAsync();
        }

        public async Task AddAsync(OrderIssue orderIssue)
        {
            await _context.OrderIssues.AddAsync(orderIssue);
        }

        public void Update(OrderIssue orderIssue)
        {
            _context.OrderIssues.Update(orderIssue);
        }

        public void Delete(OrderIssue orderIssue)
        {
            orderIssue.IsDeleted = true;
            _context.OrderIssues.Update(orderIssue);
        }

        public async Task<int> CountUserIssuesAsync(Guid userId, OrderIssueStatus status, DateTime fromDate)
        {
            return await _context.OrderIssues
                .CountAsync(x => x.UserId == userId &&
                                 x.Status == status &&
                                 x.CreatedAt >= fromDate);
        }

        public async Task<List<OrderIssue>> GetExpiredIssuesAsync(DateTime threshold)
        {
            return await _context.OrderIssues
                .Where(x => x.Status == OrderIssueStatus.InProgress && x.CreatedAt <= threshold)
                .ToListAsync();
        }

        public async Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetUserIssuesPaginatedAsync(Guid userId, OrderIssueStatus? status, int pageNumber, int pageSize)
        {
            var query = _context.OrderIssues
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Shop)
                .Where(oi => oi.UserId == userId && !oi.IsDeleted);

            if (status.HasValue)
                query = query.Where(oi => oi.Status == status.Value);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(oi => oi.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetShopIssuesPaginatedAsync(Guid shopId, OrderIssueStatus? status, int pageNumber, int pageSize)
        {
            var query = _context.OrderIssues
                .Include(oi => oi.Order)
                .Include(oi => oi.User) 
                .Where(oi => oi.Order.ShopId == shopId && !oi.IsDeleted);

            if (status.HasValue)
                query = query.Where(oi => oi.Status == status.Value);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(oi => oi.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
