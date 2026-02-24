using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class OrderVoucherRepository : IOrderVoucherRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderVoucherRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OrderVoucher>> GetByOrderIdAsync(Guid orderId)
        {
            return await _context.OrderVouchers
                .Where(ov => ov.OrderId == orderId && !ov.IsDeleted)
                .OrderBy(ov => ov.ApplyOrder)
                .Include(ov => ov.Voucher)
                .ToListAsync();
        }

        public async Task<OrderVoucher?> GetByOrderAndVoucherAsync(Guid orderId, Guid voucherId)
        {
            return await _context.OrderVouchers
                .FirstOrDefaultAsync(ov => ov.OrderId == orderId && ov.VoucherId == voucherId && !ov.IsDeleted);
        }

        public async Task AddAsync(OrderVoucher orderVoucher)
        {
            await _context.OrderVouchers.AddAsync(orderVoucher);
        }

        public void Delete(OrderVoucher orderVoucher)
        {
            // Soft delete
            orderVoucher.IsDeleted = true;
            _context.OrderVouchers.Update(orderVoucher);
        }

        public async Task DeleteAllByOrderIdAsync(Guid orderId)
        {
            var records = await _context.OrderVouchers
                .Where(ov => ov.OrderId == orderId && !ov.IsDeleted)
                .ToListAsync();

            foreach (var record in records)
            {
                record.IsDeleted = true;
            }
        }

        public void Update(OrderVoucher orderVoucher)
        {
            _context.OrderVouchers.Update(orderVoucher);
        }
    }
}
