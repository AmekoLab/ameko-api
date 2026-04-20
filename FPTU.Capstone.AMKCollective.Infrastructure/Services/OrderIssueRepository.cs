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
                    .ThenInclude(o => o.Shop)
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

        // [Fix #1] Đếm tất cả attempt hủy theo nhiều status — chuẩn e-commerce
        public async Task<int> CountUserCancelAttemptsAsync(Guid userId, IEnumerable<OrderIssueStatus> statuses, DateTime fromDate)
        {
            var statusList = statuses.ToList();
            return await _context.OrderIssues
                .CountAsync(x => x.UserId == userId &&
                                 statusList.Contains(x.Status) &&
                                 x.Type == OrderIssueType.CancelRequest &&
                                 x.CreatedAt >= fromDate);
        }

        // [Fix #2] Kiểm tra order đã có issue active chưa — tránh duplicate request
        public async Task<bool> HasActiveIssueForOrderAsync(Guid orderId)
        {
            return await _context.OrderIssues
                .AnyAsync(x => x.OrderId == orderId &&
                               (x.Status == OrderIssueStatus.InProgress || x.Status == OrderIssueStatus.Pending) &&
                               !x.IsDeleted);
        }

        public async Task<List<OrderIssue>> GetExpiredIssuesAsync(DateTime threshold)
        {
            return await _context.OrderIssues
                .Include(x => x.Order)
                    .ThenInclude(o => o.Shop)
                    .Where(x => x.Type == OrderIssueType.CancelRequest &&           // chỉ luồng hủy đơn
                                x.Status == OrderIssueStatus.InProgress &&          // bỏ AwaitingReturn ra
                               (x.UpdatedAt ?? x.CreatedAt) <= threshold &&
                               !x.IsDeleted).ToListAsync();
        }

        public async Task<List<OrderIssue>> GetExpiredIssuesByStatusAsync(OrderIssueStatus status, DateTime threshold)
        {
            return await _context.OrderIssues
                .Include(x => x.Order)
                    .ThenInclude(o => o.Shop)
                .Where(x => x.Status == status 
                             && (x.UpdatedAt ?? x.CreatedAt) <= threshold 
                             && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetAllPagedAsync(OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default)
        {
            var query = _context.OrderIssues
                .Include(oi => oi.Logs)
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.OrderItems)
                .Include(oi => oi.User)
                .Where(oi => !oi.IsDeleted);

            if (status.HasValue)
            {
                query = query.Where(oi => oi.Status == status.Value);
            }

            var totalCount = await query.CountAsync(token);
            var items = await query
                .OrderByDescending(oi => oi.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(token);

            return (items, totalCount);
        }

        public async Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default)
        {
            var query = _context.OrderIssues
                .Include(oi => oi.Logs)
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.OrderItems)
                .Include(oi => oi.User)
                .Where(oi => oi.UserId == userId && !oi.IsDeleted);

            if (status.HasValue)
            {
                query = query.Where(oi => oi.Status == status.Value);
            }

            var totalCount = await query.CountAsync(token);
            var items = await query
                .OrderByDescending(oi => oi.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(token);

            return (items, totalCount);
        }

        public async Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetByShopIdPagedAsync(Guid shopId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default)
        {
            var query = _context.OrderIssues
                .Include(oi => oi.Logs)
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.OrderItems)
                .Include(oi => oi.User)
                .Where(oi => oi.Order.ShopId == shopId && !oi.IsDeleted);

            if (status.HasValue)
            {
                query = query.Where(oi => oi.Status == status.Value);
            }

            var totalCount = await query.CountAsync(token);
            var items = await query
                .OrderByDescending(oi => oi.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(token);

            return (items, totalCount);
        }

        public Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetUserIssuesPaginatedAsync(Guid userId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default)
            => GetByUserIdPagedAsync(userId, status, pageNumber, pageSize, token);

        public Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetShopIssuesPaginatedAsync(Guid shopId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default)
            => GetByShopIdPagedAsync(shopId, status, pageNumber, pageSize, token);

        public async Task<List<OrderIssue>> GetIssuesForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default)
        {
            return await _context.OrderIssues
                .AsNoTracking()
                .Where(oi => !oi.IsDeleted && oi.CreatedAt >= fromUtc && oi.CreatedAt <= toUtc)
                .ToListAsync(token);
        }
    }
}
