using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id, CancellationToken token = default);
        Task<IEnumerable<Order>> GetOrdersByUserIdAsync(Guid userId, bool includeDeleted = false, CancellationToken token = default);
        Task<(IEnumerable<Order> orders, int totalCount)> GetOrdersByUserIdPagedAsync(Guid userId, MyOrdersFilterRequest filter, CancellationToken token = default);
        Task<IEnumerable<Order>> GetOrdersByShopIdAsync(Guid shopId, CancellationToken token = default);
        Task<int> CountInProgressOrdersForCustomerShopAsync(Guid customerId, Guid shopId, CancellationToken token = default);
        Task<int> CountMonthlyOrdersForCustomerAsync(Guid customerId, DateTime fromUtc, DateTime toUtc, CancellationToken token = default);
        Task<int> CountMonthlyOrdersForShopAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken token = default);
        Task<Order?> GetOrderByStatusAsync(Guid userId, OrderStatus status);
        /// <summary>
        /// Load cart AsNoTracking (read-only) for decision logic.
        /// Never write directly through returned entities.
        /// </summary>
        Task<Order?> GetCartOnlyAsync(Guid userId);

        /// <summary>
        /// UPDATE OrderItem quantity/price via stub (no entity load, no phantom tracking).
        /// </summary>
        void UpdateItemQuantity(Guid orderItemId, int newQuantity, decimal newUnitPrice, decimal newTotalPrice);

        /// <summary>
        /// UPDATE Order TotalAmount via stub (no entity load, no phantom tracking).
        /// </summary>
        void UpdateCartTotal(Guid orderId, decimal newTotalAmount);
        Task UpdateOrderAsync(Order order, CancellationToken token = default);
        Task<int> SaveChangesAsync(CancellationToken token = default);
        Task DeleteOrderItemAsync(Guid orderItemId);
        public void DeleteRange(IEnumerable<OrderItem> items);
        Task AddAsync(Order order, CancellationToken token = default);
        Task AddOrderItemAsync(OrderItem item);
        void Delete(Order order);
        void DeleteOrderItem(OrderItem item);
        void DetachItems(IEnumerable<OrderItem> items);
        Task<OrderItem?> FindCustomItemInCartAsync(Guid orderId, string sessionId);
        void UpdateOrderItem(OrderItem item);

        Task<IEnumerable<Order>> GetShopOrdersAsync(Guid shopId, OrderStatus? status, int page, int size, CancellationToken token = default);
        Task<IEnumerable<Order>> GetOrdersByGroupIdAsync(Guid orderGroupId, CancellationToken token = default);
        Task<List<Order>> GetOrdersEligibleForFundReleaseAsync(DateTime warrantyThreshold, CancellationToken token = default);
        Task<OrderItem?> GetOrderItemByIdAsync(Guid id, CancellationToken token = default);
        Task<List<Order>> GetOrdersPendingAssemblyInitAsync(DateTime thresholdUtc, CancellationToken token = default);

        Task<Order?> GetOrderDetailByIdAsync(Guid orderId);
        Task<List<Order>> GetAbandonedOrdersAsync(DateTime expirationTime, CancellationToken token = default);

        /// <summary>
        /// Read-only dataset for dashboard analytics.
        /// </summary>
        Task<List<Order>> GetOrdersForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);

        /// <summary>
        /// Lean shop-scoped order query for dashboard. Only includes Customer nav property.
        /// Pass null for fromUtc/toUtc to get all-time orders (used by churn analysis).
        /// </summary>
        Task<List<Order>> GetShopOrdersForDashboardAsync(Guid shopId, DateTime? fromUtc, DateTime? toUtc, CancellationToken token = default);

        /// <summary>
        /// Returns the first order date per customer for a shop, computed entirely in DB.
        /// Replaces in-memory GroupBy(allOrders) for new/returning customer logic.
        /// </summary>
        Task<Dictionary<Guid, DateTime>> GetCustomerFirstOrderDatesAsync(Guid shopId, CancellationToken token = default);

        /// <summary>
        /// Returns pre-aggregated order stats computed entirely in the database.
        /// Replaces loading all orders into memory for dashboard calculations.
        /// </summary>
        Task<OrderDashboardStats> GetOrderStatsForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);
        Task<List<AdminTopShopOrderItem>> GetTopShopsByOrderCountAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken token = default);
        Task<List<Guid>> GetPurchasedShopIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
