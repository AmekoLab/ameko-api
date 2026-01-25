using FPTU.Capstone.AMKCollective.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IOrderService
    {
        //CUSTOMER SHOPPING
        Task AddToCartAsync(Guid userId, AddToCartRequest request, CancellationToken token = default);

        Task<OrderDto> GetMyCartAsync(Guid userId, CancellationToken token = default);

        Task RemoveItemFromCartAsync(Guid userId, Guid orderItemId, CancellationToken token = default);

        Task UpdateCartItemQuantityAsync(Guid userId, Guid orderItemId, int newQuantity, CancellationToken token = default);

        Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default);

        //CUSTOMER HISTORY
        Task<List<OrderGroupDto>> GetMyOrdersAsync(Guid userId, CancellationToken token = default);

        Task<OrderGroupDto> GetOrderGroupDetailAsync(Guid orderGroupId, CancellationToken token = default);

        Task CancelOrderAsync(Guid userId, Guid orderId, string reason, CancellationToken token = default);

        //SHOP 
        Task<List<OrderDto>> GetShopOrdersAsync(Guid shopId, string? status, int page, int size, CancellationToken token = default);

        Task<OrderDto> GetShopOrderDetailAsync(Guid shopId, Guid orderId, CancellationToken token = default);

        Task UpdateOrderStatusAsync(Guid shopId, Guid orderId, string newStatus, CancellationToken token = default);
    }
}
