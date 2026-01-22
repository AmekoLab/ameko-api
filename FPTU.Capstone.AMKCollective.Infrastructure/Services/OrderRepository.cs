using FPTU.Capstone.AMKCollective.Application.Interfaces;
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
    public class OrderRepository: IOrderRepository
    {
        private readonly ApplicationDbContext _context;
        public OrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Order?> GetByIdAsync(Guid id, CancellationToken token = default)
        {
            return await _context.Orders
                .Include(o => o.Shop) 
                .Include(o => o.Customer) 
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product) 
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.OrderItemComponents)
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, token);
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(Guid userId, CancellationToken token = default)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.Shop)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.CustomerId == userId && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(token);
        }

        public async Task<IEnumerable<Order>> GetOrdersByShopIdAsync(Guid shopId, CancellationToken token = default)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer) 
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product) 
                .Where(o => o.ShopId == shopId && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(token);
        }

        public Task UpdateOrderAsync(Order order, CancellationToken token = default)
        {
            _context.Orders.Update(order);
            return Task.CompletedTask;
        }

        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }
    }
}