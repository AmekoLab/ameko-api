using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
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
        public async Task<Order?> GetOrderByStatusAsync(Guid userId, OrderStatus status)
        {
            return await _context.Orders
                .AsSplitQuery() 
                .Include(o => o.OrderItems.Where(oi => !oi.IsDeleted))
                    .ThenInclude(oi => oi.OrderItemComponents)
                .Include(o => o.OrderItems.Where(oi => !oi.IsDeleted))
                    .ThenInclude(oi => oi.Product)      
                        .ThenInclude(p => p.Shop)        
                .Include(o => o.Shop)

                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(o => o.CustomerId == userId
                                        && o.OrderStatus == status
                                        && !o.IsDeleted);
        }

        /// <summary>
        /// Load giỏ hàng AsNoTracking (READ-ONLY) — chỉ để đọc dữ liệu, quyết định logic.
        /// Không bao giờ ghi trực tiếp qua các entity được trả về.
        /// </summary>
        public async Task<Order?> GetCartOnlyAsync(Guid userId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems.Where(oi => !oi.IsDeleted))
                    .ThenInclude(oi => oi.OrderItemComponents)
                .AsSplitQuery()
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(o => o.CustomerId == userId
                                && o.OrderStatus == OrderStatus.InCart
                                && !o.IsDeleted);
        }

        /// <summary>
        /// UPDATE OrderItem quantity/price via stub entity — chỉ gửi SET Quantity, UnitPrice, TotalPrice.
        /// Không load entity, không gây phantom Modified.
        /// </summary>
        public void UpdateItemQuantity(Guid orderItemId, int newQuantity, decimal newUnitPrice, decimal newTotalPrice)
        {
            var stub = new OrderItem { Id = orderItemId };
            _context.OrderItems.Attach(stub);
            stub.Quantity = newQuantity;
            stub.UnitPrice = newUnitPrice;
            stub.TotalPrice = newTotalPrice;
            stub.UpdatedAt = DateTime.UtcNow;
            _context.Entry(stub).Property(x => x.Quantity).IsModified = true;
            _context.Entry(stub).Property(x => x.UnitPrice).IsModified = true;
            _context.Entry(stub).Property(x => x.TotalPrice).IsModified = true;
            _context.Entry(stub).Property(x => x.UpdatedAt).IsModified = true;
        }

        /// <summary>
        /// UPDATE Order TotalAmount via stub entity — chỉ gửi SET TotalAmount.
        /// Không load entity, không gây phantom Modified.
        /// </summary>
        public void UpdateCartTotal(Guid orderId, decimal newTotalAmount)
        {
            var stub = new Order { Id = orderId };
            _context.Orders.Attach(stub);
            stub.TotalAmount = newTotalAmount;
            stub.UpdatedAt = DateTime.UtcNow;
            _context.Entry(stub).Property(x => x.TotalAmount).IsModified = true;
            _context.Entry(stub).Property(x => x.UpdatedAt).IsModified = true;
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

        public async Task<IEnumerable<Order>> GetShopOrdersAsync(Guid shopId, OrderStatus? status, int page, int size, CancellationToken token = default)
        {
            var query = _context.Orders
                .Include(o => o.OrderItems) 
                .AsNoTracking() 
                .Where(o => o.ShopId == shopId);
            if (status.HasValue)
            {
                query = query.Where(o => o.OrderStatus == status);
            }
            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(token);
        }
        public void Delete(Order order)
        {
            _context.Orders.Remove(order);
        }
        public void DeleteOrderItem(OrderItem item)
        {
            _context.OrderItems.Remove(item);
        }
        public void DetachItems(IEnumerable<OrderItem> items)
        {
            foreach (var item in items)
            {
                _context.Entry(item).State = EntityState.Detached;
            }
        }

        public async Task<OrderItem?> FindCustomItemInCartAsync(Guid orderId, string sessionId)
        {
            // Viết câu lệnh SQL trực tiếp. MySQL dùng LIKE cho chuỗi.
            // {0} là orderId, {1} là pattern tìm kiếm
            var searchPattern = $"%{sessionId}%";

            var query = _context.OrderItems
                .FromSqlRaw(@"
            SELECT * FROM OrderItems 
            WHERE OrderId = {0} 
            AND IsDeleted = 0 
            AND IsCustom = 1 
            AND DesignConfig IS NOT NULL 
            AND DesignConfig LIKE {1}",
                    orderId, searchPattern);

            return await query.AsNoTracking().FirstOrDefaultAsync();
        }
        public void UpdateOrderItem(OrderItem item)
        {
            _context.OrderItems.Update(item);
        }

        public async Task<IEnumerable<Order>> GetOrdersByGroupIdAsync(Guid orderGroupId, CancellationToken token = default)
        {
            return await _context.Orders
                .Include(o => o.OrderItems) // Load items để đảm bảo tính toàn vẹn dữ liệu
                .Where(o => o.OrderGroupId == orderGroupId && !o.IsDeleted)
                .AsSplitQuery() // Tối ưu hiệu năng khi có Include
                .ToListAsync(token);
        }

        public async Task<List<Order>> GetOrdersEligibleForFundReleaseAsync(DateTime warrantyThreshold, CancellationToken token = default)
        {
            return await _context.Orders
                .Where(o => o.OrderStatus == OrderStatus.Completed
                          && o.PaymentStatus == PaymentStatus.Paid
                          && o.UpdatedAt.HasValue
                          && o.UpdatedAt.Value <= warrantyThreshold
                          && !o.IsDeleted)
                .ToListAsync(token);
        }

        public async Task<OrderItem?> GetOrderItemByIdAsync(Guid id, CancellationToken token = default)
        {
            return await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.OrderItemComponents) 
                .Include(oi => oi.Product) 
                .FirstOrDefaultAsync(oi => oi.Id == id, token);
        }

        public async Task<Order?> GetOrderDetailByIdAsync(Guid orderId)
        {
            return await _context.Orders
                .AsSplitQuery() 
                .Include(o => o.Shop) 
                .Include(o => o.OrderGroup) 
                .Include(o => o.OrderItems.Where(oi => !oi.IsDeleted))
                    .ThenInclude(oi => oi.Product) 
                .Include(o => o.OrderItems.Where(oi => !oi.IsDeleted))
                    .ThenInclude(oi => oi.OrderItemComponents) 
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        }
    }
}