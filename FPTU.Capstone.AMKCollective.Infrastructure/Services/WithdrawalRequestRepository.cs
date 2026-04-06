using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class WithdrawalRequestRepository : IWithdrawalRequestRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<WithdrawalRequest> _dbSet;

        public WithdrawalRequestRepository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<WithdrawalRequest>();
        }

        public async Task<WithdrawalRequest?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(w => w.User)
                .ThenInclude(u => u!.ShopProfile)
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<List<WithdrawalRequest>> GetByUserIdAsync(Guid userId)
        {
            return await _dbSet
                .Include(w => w.User)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.RequestedAt)
                .ToListAsync();
        }

        public async Task<List<WithdrawalRequest>> GetPendingAsync()
        {
            return await _dbSet
                .Include(w => w.User)
                .ThenInclude(u => u!.ShopProfile)
                .Where(w => w.Status == WithdrawalStatus.Pending)
                .OrderBy(w => w.RequestedAt)
                .ToListAsync();
        }

        public async Task<(List<WithdrawalRequest> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, int pageIndex, int pageSize)
        {
            var query = _dbSet.Where(w => w.UserId == userId);
            
            var totalCount = await query.CountAsync();
            var items = await query
                .Include(w => w.User)
                .OrderByDescending(w => w.RequestedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            return (items, totalCount);
        }

        public async Task<(List<WithdrawalRequest> Items, int TotalCount)> GetPendingPagedAsync(int pageIndex, int pageSize, string? shopName = null)
        {
            var query = _dbSet
                .Include(w => w.User)
                .ThenInclude(u => u!.ShopProfile)
                .Where(w => w.Status == WithdrawalStatus.Pending)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(shopName))
            {
                var pattern = $"%{shopName.Trim()}%";
                query = query.Where(w =>
                    w.User != null &&
                    w.User.ShopProfile != null &&
                    EF.Functions.Like(w.User.ShopProfile.ShopName, pattern));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(w => w.RequestedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<WithdrawalRequest> Items, int TotalCount)> GetProcessedPagedAsync(int pageIndex, int pageSize)
        {
            var query = _dbSet.Where(w => w.Status == WithdrawalStatus.Completed || w.Status == WithdrawalStatus.Rejected);
            
            var totalCount = await query.CountAsync();
            var items = await query
                .Include(w => w.User)
                .ThenInclude(u => u!.ShopProfile)
                .Include(w => w.Admin)
                .OrderByDescending(w => w.ProcessedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            return (items, totalCount);
        }

        public async Task AddAsync(WithdrawalRequest request)
        {
            await _dbSet.AddAsync(request);
        }

        public void Update(WithdrawalRequest request)
        {
            _dbSet.Update(request);
        }
    }
}
