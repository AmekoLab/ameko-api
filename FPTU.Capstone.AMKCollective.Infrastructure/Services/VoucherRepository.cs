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
    }
}