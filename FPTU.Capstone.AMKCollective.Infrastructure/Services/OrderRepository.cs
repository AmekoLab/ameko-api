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
                    .AsSplitQuery()
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
                .AsSplitQuery()
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
                .AsSplitQuery()
                .ToListAsync(token);
        }


        public async Task AddOrderItemAsync(OrderItem item)
        {
            await _context.OrderItems.AddAsync(item);
        }
        public Task UpdateOrderAsync(Order order, CancellationToken token = default)
        {
            _context.Orders.Update(order);
            return Task.CompletedTask;
        }
        public async Task<Order?> GetOrderByStatusAsync(Guid userId, string status)
        {
            return await _context.Orders
                .AsSplitQuery() 
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemComponents)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product) 
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(o => o.CustomerId == userId && o.OrderStatus == status && !o.IsDeleted);
        }

        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }
        public async Task DeleteOrderItemAsync(Guid orderItemId)
        {
            var item = await _context.OrderItems.FindAsync(orderItemId);
            if (item != null)
            {
                _context.OrderItems.Remove(item);
            }
        }
        public void DeleteRange(IEnumerable<OrderItem> items)
        {
            _context.OrderItems.RemoveRange(items);
        }
        public async Task AddAsync(Order order, CancellationToken token = default)
        {
            await _context.Orders.AddAsync(order, token);
        }
    }
}