using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
        public void Delete(VoucherUsageLog log)
        {
            _context.VoucherUsageLogs.Remove(log);
        }

        public async Task DeleteAllByOrderIdAsync(Guid orderId)
        {
            var records = await _context.VoucherUsageLogs
                .Where(ov => ov.OrderId == orderId)
                .ToListAsync();

            _context.VoucherUsageLogs.RemoveRange(records);
        }

        public void Update(VoucherUsageLog log)
        {
            _context.VoucherUsageLogs.Update(log);
        }

        public async Task<IEnumerable<VoucherUsageLog>> GetByOrderIdAsync(Guid orderId)
        {
            return await _context.VoucherUsageLogs
                .Where(ov => ov.OrderId == orderId)
                .OrderBy(ov => ov.ApplyOrder)
                .Include(ov => ov.Voucher)
                .ToListAsync();
        }

        public async Task<VoucherUsageLog?> GetByOrderAndVoucherAsync(Guid orderId, Guid voucherId)
        {
            return await _context.VoucherUsageLogs
                .FirstOrDefaultAsync(ov => ov.OrderId == orderId && ov.VoucherId == voucherId);
        }
        public async Task<(IEnumerable<VoucherUsageLog> Items, int TotalCount)> GetUsageByVoucherIdAsync(Guid voucherId, int pageNumber, int pageSize)
        {
            var query = _context.VoucherUsageLogs
                .Include(ov => ov.Order)
                    .ThenInclude(o => o.Customer)
                .Where(ov => ov.VoucherId == voucherId);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(ov => ov.Order.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<VoucherUsageLog> Items, int TotalCount)> GetAllUsagesAsync(Guid? creatorId, int pageNumber, int pageSize)
        {
            var query = _context.VoucherUsageLogs
                .Include(ov => ov.Order)
                    .ThenInclude(o => o.Customer)
                .Include(ov => ov.Voucher)
                .AsQueryable();

            if (creatorId.HasValue)
            {
                query = query.Where(ov => ov.Voucher.CreatorId == creatorId.Value);
            }

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(ov => ov.Order.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountUsageByUserAndVoucherAsync(Guid userId, Guid voucherId, Guid excludeOrderId)
        {
            return await _context.VoucherUsageLogs
                .Include(x => x.Order) 
                .CountAsync(x => x.UserId == userId
                              && x.VoucherId == voucherId
                              && x.OrderId != excludeOrderId
                              && x.Order.OrderStatus != OrderStatus.Cancelled);
        }
    }
}
