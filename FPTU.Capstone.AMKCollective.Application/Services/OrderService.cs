using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderGroupRepository _orderGroupRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly IPaymentService _paymentService;
        private readonly IMapper _mapper;
        private readonly IModelRepository _productRepo; 

        public OrderService(IOrderGroupRepository orderGroupRepo, IOrderRepository orderRepo, IPaymentService paymentService, IMapper mapper, IModelRepository productRepo)
        {
            _orderGroupRepo = orderGroupRepo;
            _orderRepo = orderRepo;
            _paymentService = paymentService;
            _mapper = mapper;
            _productRepo = productRepo;
        }
        public async Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default)
        {
            var orderGroup = new OrderGroup
            {
                Id = Guid.NewGuid(),
                PaymentStatus = "Pending",
                CreatedAt = DateTime.UtcNow,
                TotalGroupAmount = 0
            };

            var itemsByShop = request.Items.GroupBy(i => i.ShopId);
            foreach(var shopGroup in itemsByShop)
            {
                var shopId = shopGroup.Key;
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderGroupId = orderGroup.Id,
                    CustomerId = userId,
                    ShopId = shopId,

                    ReceiverName = request.ReceiverName,
                    ReceiverPhone = request.ReceiverPhone,
                    ShippingAddress = request.ShippingAddress,
                    Note = request.Note,

                    OrderStatus = "Pending",
                    PaymentStatus = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                decimal subTotal = 0;

                foreach (var itemDto in shopGroup)
                {
                    decimal itemDiscount = 0;
                    var product = await _productRepo.GetByIdAsync(itemDto.ProductId);
                    if (product == null) throw new Exception($"item {itemDto.ProductId} not exist ");
                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = itemDto.ProductId,
                        //Snapshot
                        ProductName = product.Name,
                        ProductImage = product.ThumbnailURL,

                        Quantity = itemDto.Quantity,
                        UnitPrice = product.Price,
                        DiscountAmount = itemDiscount,

                        TotalPrice = (itemDto.UnitPrice * itemDto.Quantity) - itemDiscount,

                        IsCustom = itemDto.IsCustom,
                        DesignConfig = (itemDto.IsCustom && itemDto.CustomComponentIds != null)
                        ? JsonSerializer.Serialize(itemDto.CustomComponentIds) : null,
                        Notes = null

                    };

                    order.OrderItems.Add(orderItem);
                    subTotal += orderItem.TotalPrice;
                }

                order.SubTotal = subTotal;
                order.ShippingFee = 30000; //TODO: change hardcode
                order.DiscountAmount = 0;

                order.TotalAmount = order.SubTotal + order.ShippingFee - order.DiscountAmount;
                if (order.TotalAmount < 0)
                {
                    order.TotalAmount = 0;
                }
                orderGroup.Orders.Add(order);
                orderGroup.TotalGroupAmount += order.TotalAmount;
            }

            await _orderGroupRepo.CreateAsync(orderGroup, token);
            await _orderGroupRepo.SaveChangesAsync(token);
            var paymentResponse = await _paymentService.CreateCheckoutSessionAsync(orderGroup.Id, token);

            return new CheckoutResponse
            {
                OrderGroupId = orderGroup.Id,
                TotalAmount = orderGroup.TotalGroupAmount,
                PaymentUrl = paymentResponse.PaymentUrl,
            };
        }

        public async Task<List<OrderGroupDto>> GetMyOrdersAsync(Guid userId, CancellationToken token = default)
        {
            var orderGroup = await _orderGroupRepo.GetByUserIdAsync(userId, token);
            return _mapper.Map<List<OrderGroupDto>>(orderGroup);
        }

        public async Task<OrderGroupDto> GetOrderGroupDetailAsync(Guid orderGroupId, CancellationToken token = default)
        {
            var orderGroup = await _orderGroupRepo.GetByIdAsync(orderGroupId, token);
            if (orderGroup == null)
            {
                throw new KeyNotFoundException("Order not found");

            }
            return _mapper.Map<OrderGroupDto>(orderGroup);
        }

        public Task CancelOrderAsync(Guid userId, Guid orderId, string reason, CancellationToken token = default) { 
            //TODO: implement
            //logic check status -> update status -> save
            throw new NotImplementedException();
            
        }

        public async Task<List<OrderDto>> GetShopOrdersAsync(Guid shopId, string? status, int page, int size, CancellationToken token = default)
        {
            var orders = await _orderRepo.GetOrdersByShopIdAsync(shopId, token);
            if (!string.IsNullOrEmpty(status))
            {
                orders = orders.Where(o => o.OrderStatus.Equals(status, StringComparison.OrdinalIgnoreCase));
            }
            var pagedOrders = orders
                .Skip((page - 1) * size)
                .Take(size)
                .ToList();

            return _mapper.Map<List<OrderDto>>(pagedOrders);
                
        }

        public async Task UpdateOrderStatusAsync(Guid shopId, Guid orderId, string newStatus, CancellationToken token = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, token);
            if (order == null || order.ShopId != shopId)
            {
                throw new KeyNotFoundException("Order not found");
            }

            order.OrderStatus = newStatus;

            await _orderRepo.UpdateOrderAsync(order, token);
            await _orderRepo.SaveChangesAsync(token);
        }
        public async Task<OrderDto> GetShopOrderDetailAsync(Guid shopId, Guid orderId, CancellationToken token = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, token);

            if (order == null || order.ShopId != shopId)
                throw new KeyNotFoundException("Order not found or access denied");

            return _mapper.Map<OrderDto>(order);
        }
    }
}
   