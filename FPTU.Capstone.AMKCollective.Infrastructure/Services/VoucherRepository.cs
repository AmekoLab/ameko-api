using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class VoucherRepository : IVoucherRepository
    {
        private readonly ApplicationDbContext _context;

        public VoucherRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Voucher?> GetByIdAsync(Guid id)
        {
            return await _context.Vouchers
                .Include(v => v.Creator) // Include Shop info
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
        }

        public async Task<Voucher?> GetByCodeAsync(string code)
        {
            return await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == code && !v.IsDeleted);
        }

        public async Task<IEnumerable<Voucher>> GetByCreatorIdAsync(Guid creatorId)
        {
            return await _context.Vouchers
                .Where(v => v.CreatorId == creatorId && !v.IsDeleted)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Voucher>> GetValidVouchersForUserAsync(Guid userId)
        {
            var now = DateTime.Now;

            // Logic: Lấy voucher chưa xóa, Active, Trong thời hạn
            // VÀ (Là voucher public HOẶC Là voucher riêng của user này)
            return await _context.Vouchers
                .Include(v => v.Creator)
                .ThenInclude(u => u.ShopProfile)
                .Where(v => !v.IsDeleted &&
                            v.Status == VoucherStatus.Active &&
                            v.StartDate <= now &&
                            v.EndDate >= now &&
                            (v.TargetUserId == null || v.TargetUserId == userId))
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Voucher voucher)
        {
            await _context.Vouchers.AddAsync(voucher);
        }

        public void Update(Voucher voucher)
        {
            _context.Vouchers.Update(voucher);
        }

        public void Delete(Voucher voucher)
        {
            // Soft Delete
            voucher.IsDeleted = true;
            _context.Vouchers.Update(voucher);
        }

        public async Task<bool> IsVoucherUsedAsync(Guid voucherId)
        {
            // Kiểm tra trong bảng Log xem có record nào không
            return await _context.VoucherUsageLogs.AnyAsync(x => x.VoucherId == voucherId && !x.IsDeleted);
        }

        public async Task<(IEnumerable<Voucher> Items, int TotalCount)> GetVouchersByFilterAsync(Guid? creatorId, VoucherFilterRequest filter)
        {
            var query = _context.Vouchers.Where(v => !v.IsDeleted);

            // Nếu có ID (Shop gọi) -> Lọc đúng mã của Shop đó
            // Nếu ID null (Admin gọi) -> Bỏ qua bước lọc này, lấy toàn bộ
            if (creatorId.HasValue)
            {
                query = query.Where(v => v.CreatorId == creatorId.Value);
            }

            // 1. Filter by Code/Name
            if (!string.IsNullOrEmpty(filter.SearchCode))
            {
                var text = filter.SearchCode.ToLower();
                query = query.Where(v => v.Code.ToLower().Contains(text) || v.Name.ToLower().Contains(text));
            }

            // 2. Filter by Status
            if (filter.Status.HasValue)
            {
                query = query.Where(v => v.Status == filter.Status.Value);
            }

            // 3. Filter Expired
            if (filter.IsExpired.HasValue)
            {
                var now = DateTime.UtcNow; 
                if (filter.IsExpired.Value)
                    query = query.Where(v => v.EndDate < now);
                else
                    query = query.Where(v => v.EndDate >= now);
            }

            // 4. Date Range
            if (filter.FromDate.HasValue)
                query = query.Where(v => v.StartDate >= filter.FromDate.Value);
            if (filter.ToDate.HasValue)
                query = query.Where(v => v.StartDate <= filter.ToDate.Value);

            // Count total before paging
            var totalCount = await query.CountAsync();

            // Paging & Sort
            var items = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<Voucher>> GetPublicVouchersByShopAsync(Guid shopUserId)
        {
            var now = DateTime.UtcNow;
            return await _context.Vouchers
                .Where(v => v.CreatorId == shopUserId 
                            && !v.IsDeleted
                            && v.Status == VoucherStatus.Active
                            && v.StartDate <= now 
                            && v.EndDate >= now
                            && v.TargetUserId == null // Chỉ lấy voucher không active riêng cho ai
                            && v.UsedCount < v.UsageLimit) // Còn lượt dùng
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> TryIncrementVoucherUsageAsync(Guid voucherId)
        {
            var rowsAffected = await _context.Vouchers
                .Where(v => v.Id == voucherId && v.UsedCount < v.UsageLimit)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1));

            return rowsAffected > 0;
        }
    }
}
    