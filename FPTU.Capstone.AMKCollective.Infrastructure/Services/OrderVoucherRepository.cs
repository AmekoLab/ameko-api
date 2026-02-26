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
                .Where(ov => ov.OrderId == orderId)
                .OrderBy(ov => ov.ApplyOrder)
                .Include(ov => ov.Voucher)
                .ToListAsync();
        }

        public async Task<OrderVoucher?> GetByOrderAndVoucherAsync(Guid orderId, Guid voucherId)
        {
            return await _context.OrderVouchers
                .FirstOrDefaultAsync(ov => ov.OrderId == orderId && ov.VoucherId == voucherId);
        }

        public async Task AddAsync(OrderVoucher orderVoucher)
        {
            await _context.OrderVouchers.AddAsync(orderVoucher);
        }

        public void Delete(OrderVoucher orderVoucher)
        {
            _context.OrderVouchers.Remove(orderVoucher);
        }

        public async Task DeleteAllByOrderIdAsync(Guid orderId)
        {
            var records = await _context.OrderVouchers
                .Where(ov => ov.OrderId == orderId)
                .ToListAsync();

            _context.OrderVouchers.RemoveRange(records);
        }

        public void Update(OrderVoucher orderVoucher)
        {
            _context.OrderVouchers.Update(orderVoucher);
        }
    }
}
