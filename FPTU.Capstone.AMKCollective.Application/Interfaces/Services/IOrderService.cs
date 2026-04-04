using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IOrderService
    {
        //CUSTOMER SHOPPING
        Task AddToCartAsync(Guid userId, AddToCartRequest request, CancellationToken token = default);

        Task<OrderResponse> GetMyCartAsync(Guid userId, CancellationToken token = default);

        Task RemoveItemFromCartAsync(Guid userId, Guid orderItemId, CancellationToken token = default);

        Task UpdateCartItemQuantityAsync(Guid userId, Guid orderItemId, int newQuantity, CancellationToken token = default);

        Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default);
        Task<CalculateCartResponse> CalculateCartPreviewAsync(Guid userId, CalculateCartRequest request);

        //CUSTOMER HISTORY
        Task<List<OrderResponse>> GetMyOrdersAsync(Guid userId, CancellationToken token = default);
        Task<List<OrderGroupResponse>> GetMyOrderGroupsAsync(Guid userId, CancellationToken token = default);

        Task<OrderGroupResponse> GetOrderGroupDetailAsync(Guid orderGroupId, CancellationToken token = default);

        Task CancelOrderAsync(Guid userId, Guid orderId, string reason, CancellationToken token = default);

        //SHOP 
        Task<List<OrderResponse>> GetShopOrdersAsync(Guid shopId, OrderStatus? status, int page, int size, CancellationToken token = default);

        Task<OrderResponse> GetShopOrderDetailAsync(Guid shopId, Guid orderId, CancellationToken token = default);
        Task CancelOrderByShopAsync(Guid shopId, Guid orderId, string reason, CancellationToken token = default);

        Task UpdateOrderStatusAsync(Guid shopId, Guid orderId, DTOs.Order.UpdateOrderStatusRequest request, CancellationToken token = default);

        Task<OrderIssueResponse> RequestCancelOrderAsync(Guid userId, DTOs.OrderIssues.CancelOrderRequest request, CancellationToken token = default);

        Task ProcessCancelRequestAsync(Guid userId, ProcessIssueRequest request, CancellationToken token = default);

        Task<CheckoutResponse> RepayAsync(Guid userId, RepayRequest request, CancellationToken token = default);
        Task<OrderResponse> GetOrderDetailAsync(Guid userId, Guid orderId);
        Task UpdateShippingAddressAsync(Guid userId, Guid orderId, DTOs.Order.UpdateShippingAddressRequest request, CancellationToken token = default);

        // BACKGROUND WORKER - Release held funds after warranty period
        Task ReleaseFundsForEligibleOrdersAsync(CancellationToken token = default);
        Task CancelAbandonedOrdersAsync(CancellationToken token = default);
        Task AutoCancelOrdersWithoutAssemblyAsync(CancellationToken token = default);
        

        }
}
