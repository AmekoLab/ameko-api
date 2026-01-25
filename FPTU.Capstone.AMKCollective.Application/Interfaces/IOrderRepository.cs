using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id, CancellationToken token = default);
        Task<IEnumerable<Order>> GetOrdersByUserIdAsync(Guid userId, CancellationToken token = default);
        Task<IEnumerable<Order>> GetOrdersByShopIdAsync(Guid shopId, CancellationToken token = default);
        Task<Order?> GetOrderByStatusAsync(Guid userId, string status);
        Task UpdateOrderAsync(Order order, CancellationToken token = default);
        Task<int> SaveChangesAsync(CancellationToken token = default);
        Task DeleteOrderItemAsync(Guid orderItemId);
        public void DeleteRange(IEnumerable<OrderItem> items);
        Task AddAsync(Order order, CancellationToken token = default);
        Task AddOrderItemAsync(OrderItem item);

        Task<IEnumerable<Order>> GetShopOrdersAsync(Guid shopId, string? status, int page, int size, CancellationToken token = default);
    }
}
