using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private readonly IBuilderSessionRepository _builderSessionRepo; 
        public OrderService(IOrderGroupRepository orderGroupRepo, IOrderRepository orderRepo, IPaymentService paymentService, IMapper mapper, IModelRepository productRepo, IBuilderSessionRepository builderSessionRepo)
        {
            _orderGroupRepo = orderGroupRepo;
            _orderRepo = orderRepo;
            _paymentService = paymentService;
            _mapper = mapper;
            _productRepo = productRepo;
            _builderSessionRepo = builderSessionRepo;
        }
        public async Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default)
        {
            // 1. LẤY GIỎ HÀNG TỪ DB
            var cartOrder = await _orderRepo.GetOrderByStatusAsync(userId, "InCart");

            if (cartOrder == null || !cartOrder.OrderItems.Any())
            {
                throw new Exception("Cart empty cannot checkout");
            }

            // 2. TẠO ORDER GROUP (Đơn hàng tổng)
            var orderGroup = new OrderGroup
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                PaymentStatus = "Pending",
                CreatedAt = DateTime.UtcNow,
                TotalGroupAmount = 0,
                Orders = new List<Order>()
            };

            // 3. NHÓM THEO SHOP
            // Group các món hàng theo ShopId để tạo các đơn con tương ứng
            var itemsByShop = cartOrder.OrderItems.GroupBy(i =>
            {
                return (i.Product?.ShopId == null || i.Product.ShopId == Guid.Empty)
                        ? (Guid?)null
                        : i.Product.ShopId;
            });

            foreach (var shopGroup in itemsByShop)
            {
                var shopId = shopGroup.Key;


                // Tạo đơn con (Order)
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
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = new List<OrderItem>()
                };
                decimal subTotal = 0;

                foreach (var cartItem in shopGroup)
                {
                    // Tạo OrderItem mới (Snapshot từ giỏ hàng)
                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = cartItem.ProductId,
                        ProductName = cartItem.ProductName,
                        ProductImage = cartItem.ProductImage?? string.Empty,
                        UnitPrice = cartItem.UnitPrice,
                        Quantity = cartItem.Quantity,
                        TotalPrice = cartItem.TotalPrice,

                        IsCustom = cartItem.IsCustom,
                        DesignConfig = cartItem.DesignConfig,
                        OrderItemComponents = new List<OrderItemComponent>()
                    };

                    // Copy linh kiện (Components) nếu là hàng Custom
                    if (cartItem.OrderItemComponents != null && cartItem.OrderItemComponents.Any())
                    {
                        foreach (var comp in cartItem.OrderItemComponents)
                        {
                            // LƯU Ý: Ở đây copy dữ liệu từ `comp` (item trong giỏ) sang đơn mới
                            orderItem.OrderItemComponents.Add(new OrderItemComponent
                            {
                                Id = Guid.NewGuid(),
                                OrderItemId = orderItem.Id,

                                // Copy chính xác tên field từ Entity OrderItemComponent
                                PartId = comp.PartId,
                                PartName = comp.PartName,
                                PartPriceSnapshot = comp.PartPriceSnapshot,
                                PartImageUrl = comp.PartImageUrl ?? string.Empty,
                                Quantity = comp.Quantity
                            });
                        }
                    }

                    order.OrderItems.Add(orderItem);
                    subTotal += orderItem.TotalPrice;
                }

                // Tính toán tổng tiền cho đơn hàng con
                order.SubTotal = subTotal;
                order.ShippingFee = 30000; // Hardcode phí ship (Logic thực tế nên tính dynamic)
                order.DiscountAmount = 0;

                order.TotalAmount = order.SubTotal + order.ShippingFee - order.DiscountAmount;
                if (order.TotalAmount < 0) order.TotalAmount = 0;

                orderGroup.Orders.Add(order);
                orderGroup.TotalGroupAmount += order.TotalAmount;
            }

            // 4. LƯU ĐƠN HÀNG THẬT VÀO DB
            await _orderGroupRepo.CreateAsync(orderGroup, token);

            // 5. XÓA GIỎ HÀNG (QUAN TRỌNG)
            // Sau khi đã tạo đơn thành công, xóa sạch các món trong giỏ hàng InCart
            _orderRepo.DeleteRange(cartOrder.OrderItems);

            // (Tùy chọn: Reset tổng tiền giỏ hàng về 0 nếu cần giữ xác Order InCart)
            cartOrder.TotalAmount = 0;

            // Lưu tất cả thay đổi (Tạo OrderGroup + Xóa Cart Items)
            await _orderRepo.SaveChangesAsync(token);

            // 6. TẠO LINK THANH TOÁN STRIPE
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

        public Task CancelOrderAsync(Guid userId, Guid orderId, string reason, CancellationToken token = default)
        {
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

        public async Task<OrderDto> GetMyCartAsync(Guid userId)
        {
            var cartOrder = await _orderRepo.GetOrderByStatusAsync(userId, "InCart");
            return cartOrder == null ? null : _mapper.Map<OrderDto>(cartOrder);
        }

        public async Task AddToCartAsync(Guid userId, AddToCartRequest request)
        {
            if (request.Quantity <= 0)
            {
                request.Quantity = 1;
            }
            var cartOrder = await _orderRepo.GetOrderByStatusAsync(userId, "InCart");

            if (cartOrder == null)
            {
                cartOrder = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = userId,
                    OrderStatus = "InCart",
                    PaymentStatus = "Unpaid",
                    TotalAmount = 0,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = new List<OrderItem>(),
                    ShopId = null
                };
                await _orderRepo.AddAsync(cartOrder);
                await _orderRepo.SaveChangesAsync();
            }

            OrderItem newItem = null;
            bool isUpdateQuantity = false;

            // =========================================================
            // CASE 1: BUILDER SESSION (Custom Keyboard)
            // =========================================================
            if (request.BuilderSessionId.HasValue && request.BuilderSessionId != Guid.Empty)
            {
                var session = await _builderSessionRepo.GetSessionByIdAsync(request.BuilderSessionId.Value);
                if (session == null) throw new Exception("Builder session not found or expired");
                var existingCustomItem = cartOrder.OrderItems.FirstOrDefault(oi =>
                    oi.IsCustom == true &&
                    !string.IsNullOrEmpty(oi.DesignConfig) &&
                    oi.DesignConfig.Contains(session.Id.ToString()) 
                );

                if (existingCustomItem != null)
                {
                    existingCustomItem.Quantity += request.Quantity;
                    existingCustomItem.TotalPrice = existingCustomItem.UnitPrice * existingCustomItem.Quantity;
                    isUpdateQuantity = true;
                }
                else
                {
                    var selection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartDetail>>(session.SelectedItemsJson);

                    newItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = cartOrder.Id,
                        ProductId = session.BaseKitId,
                        ProductName = $"{session.BaseKit.Name} (Custom Build)",
                        ProductImage = session.BaseKit.ThumbnailURL ?? string.Empty,
                        UnitPrice = session.BaseKit.Price,
                        Quantity = request.Quantity,
                        IsCustom = true,
                        DesignConfig = JsonSerializer.Serialize(new { SessionId = session.Id }),
                        OrderItemComponents = new List<OrderItemComponent>()
                    };

                    if (selection != null)
                    {
                        foreach (var part in selection.Values)
                        {
                            newItem.OrderItemComponents.Add(new OrderItemComponent
                            {
                                Id = Guid.NewGuid(),
                                OrderItemId = newItem.Id,
                                PartId = part.Id,
                                PartName = part.Name,
                                PartPriceSnapshot = part.Price,
                                PartImageUrl = part.ThumbnailUrl ?? string.Empty,
                                Quantity = 1
                            });
                            newItem.UnitPrice += part.Price;
                        }
                    }
                    newItem.TotalPrice = newItem.UnitPrice * newItem.Quantity;
                }
            }
            // =========================================================
            // CASE 2: NORMAL PRODUCT (Sản phẩm thường)
            // =========================================================
            else
            {
                if (request.ProductId == null) throw new Exception("Product ID is required.");

                var product = await _productRepo.GetByIdAsync(request.ProductId.Value);
                if (product == null) throw new Exception("Product not found");

                var existingItem = cartOrder.OrderItems
                    .FirstOrDefault(oi => oi.ProductId == request.ProductId.Value && !oi.IsCustom);

                if (existingItem != null)
                {
                    existingItem.Quantity += request.Quantity;
                    existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
                    isUpdateQuantity = true;
                }
                else
                {
                    newItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = cartOrder.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductImage = product.ThumbnailURL ?? string.Empty,
                        UnitPrice = product.Price,
                        Quantity = request.Quantity,
                        IsCustom = request.IsCustom,
                        TotalPrice = product.Price * request.Quantity,
                        DesignConfig = (request.IsCustom && request.CustomComponentIds != null)
                            ? JsonSerializer.Serialize(request.CustomComponentIds) : null,
                        OrderItemComponents = new List<OrderItemComponent>()
                    };

                    if (request.IsCustom && request.CustomComponentIds != null)
                    {
                        foreach (var compId in request.CustomComponentIds)
                        {
                            var component = await _productRepo.GetByIdAsync(compId);
                            if (component != null)
                            {
                                newItem.OrderItemComponents.Add(new OrderItemComponent
                                {
                                    Id = Guid.NewGuid(),
                                    OrderItemId = newItem.Id,
                                    PartId = component.Id,
                                    PartName = component.Name,
                                    PartPriceSnapshot = component.Price,
                                    PartImageUrl = component.ThumbnailURL ?? string.Empty,
                                    Quantity = 1
                                });
                            }
                        }
                    }
                }
            }

            // 3. LƯU VÀO DB
            if (newItem != null && !isUpdateQuantity)
            {
                await _orderRepo.AddOrderItemAsync(newItem);
                cartOrder.OrderItems.Add(newItem);
            }
            cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
            await _orderRepo.SaveChangesAsync();
        }

        public async Task RemoveItemFromCartAsync(Guid userId, Guid orderItemId)
        {
            var cartOrder = await _orderRepo.GetOrderByStatusAsync(userId, "InCart");
            if (cartOrder == null) throw new Exception("Cart is empty");

            var itemToRemove = cartOrder.OrderItems.FirstOrDefault(x => x.Id == orderItemId);

            if (itemToRemove != null)
            {
                await _orderRepo.DeleteOrderItemAsync(itemToRemove.Id);
                cartOrder.OrderItems.Remove(itemToRemove);
                if (cartOrder.OrderItems.Count == 0)
                {
                    cartOrder.TotalAmount = 0;
                    cartOrder.IsDeleted = true; 
                }
                else
                {
                    cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
                }
                await _orderRepo.SaveChangesAsync();
            }
        }

        public async Task UpdateCartItemQuantityAsync(Guid userId, Guid orderItemId, int newQuantity)
        {
            if (newQuantity <= 0)
            {
                await RemoveItemFromCartAsync(userId, orderItemId);
                return;
            }
            var cartOrder = await _orderRepo.GetOrderByStatusAsync(userId, "InCart");
            if (cartOrder == null) throw new Exception("Cart empty");
            var item = cartOrder.OrderItems.FirstOrDefault(x => x.Id == orderItemId);
            if (item == null) throw new Exception($"Item not found (ID: {orderItemId})");
            item.Quantity = newQuantity;
            item.TotalPrice = item.UnitPrice * item.Quantity;

            cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);

            await _orderRepo.SaveChangesAsync();
        }


    }
}