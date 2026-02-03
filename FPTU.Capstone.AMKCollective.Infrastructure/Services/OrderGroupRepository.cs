using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class OrderGroupRepository: IOrderGroupRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderGroupRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<OrderGroup?> GetByIdAsync(Guid id, CancellationToken token = default)
        {
            return await _context.OrderGroups
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderItems) 
                .FirstOrDefaultAsync(og => og.Id == id && !og.IsDeleted, token);
        }

        public async Task CreateAsync(OrderGroup orderGroup, CancellationToken token = default)
        {
            await _context.OrderGroups.AddAsync(orderGroup, token);
        }

        public async Task UpdatePaymentStatusAsync(Guid orderGroupId, string status, CancellationToken token = default)
        {
            await _context.OrderGroups
                .Where(og => og.Id == orderGroupId)
                .ExecuteUpdateAsync(s => s.SetProperty(og => og.PaymentStatus, status), token);
        }

        public async Task<IEnumerable<OrderGroup>> GetByUserIdAsync(Guid userId, CancellationToken token = default)
        {
            return await _context.OrderGroups
                .AsNoTracking()
                .Include(og => og.Orders)
                    .ThenInclude(o => o.Shop)
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                .Include(og => og.Payments)
                .Where(og => og.Orders.Any(o => o.CustomerId == userId) && !og.IsDeleted)
                .OrderByDescending(og => og.CreatedAt)
                .ToListAsync(token);
        }
        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }
        public async Task UpdateAsync(OrderGroup orderGroup)
        {
            _context.OrderGroups.Update(orderGroup);
            await Task.CompletedTask;
        }
    }
}
