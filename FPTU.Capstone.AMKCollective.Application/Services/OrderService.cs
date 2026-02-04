using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IPaymentService _paymentService;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper, IPaymentService paymentService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _paymentService = paymentService;
        }

        // =================================================================
        // 1. SHOPPING CART (ADD, GET, REMOVE, UPDATE)
        // =================================================================

        public async Task AddToCartAsync(Guid userId, AddToCartRequest request, CancellationToken token = default)
        {
            if (request.Quantity <= 0) request.Quantity = 1;

            Model product = null;

            // 1. Lấy Product
            if (request.BuilderSessionId.HasValue)
            {
                var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.BuilderSessionId.Value);
                if (session == null) throw new KeyNotFoundException("Builder session not found.");
                product = await _unitOfWork.Models.GetByIdAsync(session.BaseKitId);
            }
            else
            {
                if (request.ProductId == null) throw new ArgumentNullException(nameof(request.ProductId));
                product = await _unitOfWork.Models.GetByIdAsync(request.ProductId.Value);
            }

            // 2. Validate
            if (product == null) throw new KeyNotFoundException("Product not found.");
            if (!product.IsActive) throw new InvalidOperationException("Product is inactive.");
            if (product.Shop != null && (product.Shop.Status != ShopStatus.Active || !product.Shop.IsActive))
                throw new InvalidOperationException("Shop unavailable.");
            if (product.StockQuantity < request.Quantity)
                throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");

            // 3. Get/Create Cart
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null)
            {
                cartOrder = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = userId,
                    OrderStatus = OrderStatus.InCart,
                    PaymentStatus = PaymentStatus.Pending,
                    TotalAmount = 0,
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = new List<OrderItem>()
                };
                await _unitOfWork.Orders.AddAsync(cartOrder);
            }

            // 4. Add Item logic
            if (request.BuilderSessionId.HasValue)
                await AddCustomItemToCartAsync(cartOrder, request, product);
            else
                await AddNormalItemToCartAsync(cartOrder, request, product);

            cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
            await _unitOfWork.CommitAsync();
        }

        public async Task<OrderResponse> GetMyCartAsync(Guid userId, CancellationToken token = default)
        {
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null) return null; // Hoặc trả về new OrderDto rỗng
            return _mapper.Map<OrderResponse>(cartOrder);
        }

        public async Task RemoveItemFromCartAsync(Guid userId, Guid orderItemId, CancellationToken token = default)
        {
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null) throw new KeyNotFoundException("Cart is empty.");

            var item = cartOrder.OrderItems.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null) throw new KeyNotFoundException("Item not found in cart.");

            // Xóa item
            // Lưu ý: Nếu EF Core Tracking enabled, chỉ cần Remove khỏi List và SaveChanges
            cartOrder.OrderItems.Remove(item);

            // Nếu cần gọi repo delete explicit:
            // await _unitOfWork.Orders.DeleteOrderItemAsync(item); (Nếu có hàm này)

            // Recalculate Total
            cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateCartItemQuantityAsync(Guid userId, Guid orderItemId, int newQuantity, CancellationToken token = default)
        {
            if (newQuantity <= 0)
            {
                await RemoveItemFromCartAsync(userId, orderItemId, token);
                return;
            }

            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null) throw new KeyNotFoundException("Cart is empty.");

            var item = cartOrder.OrderItems.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null) throw new KeyNotFoundException("Item not found.");

            // Check Stock Realtime
            var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
            if (product != null)
            {
                if (product.StockQuantity < newQuantity)
                    throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");

                // Update Price Realtime (Tránh lỗi giá cũ)
                item.UnitPrice = product.Price;
            }

            item.Quantity = newQuantity;
            item.TotalPrice = item.Quantity * item.UnitPrice;

            cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // 2. CHECKOUT & CUSTOMER ORDERS
        // =================================================================

        public async Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default)
        {
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null || !cartOrder.OrderItems.Any())
                throw new InvalidOperationException("Cart is empty.");

            string successUrl = request.SuccessUrl;
            string cancelUrl = request.CancelUrl;

            if (string.IsNullOrEmpty(successUrl) || !successUrl.StartsWith("http"))
                successUrl = "http://localhost:3000/payment/success";

            if (string.IsNullOrEmpty(cancelUrl) || !cancelUrl.StartsWith("http"))
                cancelUrl = "http://localhost:3000/payment/cancel";

            var orderGroup = new OrderGroup
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TotalGroupAmount = 0,
                Orders = new List<Order>()
            };

            var itemsByShop = cartOrder.OrderItems.GroupBy(i => i.Product?.ShopId ?? Guid.Empty);

            foreach (var shopGroup in itemsByShop)
            {
                if (shopGroup.Key == Guid.Empty) continue;

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderGroupId = orderGroup.Id,
                    CustomerId = userId,
                    ShopId = shopGroup.Key,
                    ReceiverName = request.ReceiverName,
                    ReceiverPhone = request.ReceiverPhone,
                    ShippingAddress = request.ShippingAddress,
                    Note = request.Note,
                    OrderStatus = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = new List<OrderItem>()
                };

                decimal subTotal = 0;

                foreach (var cartItem in shopGroup)
                {
                    var product = await _unitOfWork.Models.GetByIdAsync(cartItem.ProductId);

                    if (product == null) throw new InvalidOperationException($"Product {cartItem.ProductName} missing.");
                    if (product.StockQuantity < cartItem.Quantity)
                        throw new InvalidOperationException($"Out of stock: {product.Name}");

                    product.StockQuantity -= cartItem.Quantity;
                    await _unitOfWork.Models.UpdateAsync(product);

                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = cartItem.ProductId,
                        ProductName = cartItem.ProductName,
                        ProductImage = cartItem.ProductImage,
                        UnitPrice = cartItem.UnitPrice,
                        Quantity = cartItem.Quantity,
                        TotalPrice = cartItem.TotalPrice,
                        IsCustom = cartItem.IsCustom,
                        DesignConfig = cartItem.DesignConfig,
                        OrderItemComponents = new List<OrderItemComponent>()
                    };

                    if (cartItem.OrderItemComponents != null && cartItem.OrderItemComponents.Any())
                    {
                        foreach (var comp in cartItem.OrderItemComponents)
                        {
                            var partEntity = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                            if (partEntity == null) throw new InvalidOperationException($"Component {comp.PartName} not found.");

                            int requiredQtyPerKit = 1;
                            if (!string.IsNullOrEmpty(product.Specifications))
                            {
                                try
                                {
                                    using (JsonDocument doc = JsonDocument.Parse(product.Specifications))
                                    {
                                        if (doc.RootElement.TryGetProperty("recipe", out JsonElement recipe))
                                        {
                                            if (partEntity.Category != null)
                                            {
                                                string partCategorySlug = partEntity.Category.Slug.ToLower();
                                                foreach (var property in recipe.EnumerateObject())
                                                {
                                                    if (partCategorySlug == property.Name.ToLower())
                                                    {
                                                        requiredQtyPerKit = property.Value.GetInt32();
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { /* Ignore JSON errors */ }
                            }
                            // -------------------------------------

                            int totalPartNeeded = requiredQtyPerKit * cartItem.Quantity;

                            if (partEntity.StockQuantity < totalPartNeeded)
                            {
                                throw new InvalidOperationException($"Không đủ linh kiện '{partEntity.Name}'. Cần: {totalPartNeeded}, Còn: {partEntity.StockQuantity}");
                            }

                            partEntity.StockQuantity -= totalPartNeeded;
                            await _unitOfWork.Models.UpdateAsync(partEntity);

                            orderItem.OrderItemComponents.Add(new OrderItemComponent
                            {
                                Id = Guid.NewGuid(),
                                OrderItemId = orderItem.Id,
                                PartId = comp.PartId,
                                PartName = comp.PartName,
                                PartPriceSnapshot = comp.PartPriceSnapshot,
                                PartImageUrl = comp.PartImageUrl,
                                Quantity = requiredQtyPerKit 
                            });
                        }
                    }

                    order.OrderItems.Add(orderItem);
                    subTotal += orderItem.TotalPrice;
                }

                order.SubTotal = subTotal;
                order.ShippingFee = 30000;
                order.TotalAmount = order.SubTotal + order.ShippingFee;

                orderGroup.Orders.Add(order);
                orderGroup.TotalGroupAmount += order.TotalAmount;
            }
            await _unitOfWork.OrderGroups.CreateAsync(orderGroup);
            await _unitOfWork.CommitAsync();
            try
            {
                var paymentRequest = new CreateCheckoutSessionRequest
                {
                    OrderGroupId = orderGroup.Id,
                    SuccessUrl = successUrl, 
                    CancelUrl = cancelUrl
                };

                var paymentRes = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);

                _unitOfWork.Orders.Delete(cartOrder);
                await _unitOfWork.CommitAsync(); 

                return new CheckoutResponse
                {
                    OrderGroupId = orderGroup.Id,
                    TotalAmount = orderGroup.TotalGroupAmount,
                    PaymentUrl = paymentRes.PaymentUrl
                };
            }
            catch (Exception ex)
            {
                
                throw new InvalidOperationException($"Payment error: {ex.Message}. please try again.");
            }
        }

        public async Task<List<OrderResponse>> GetMyOrdersAsync(Guid userId, CancellationToken token = default)
        {
            // Gọi Repo lấy Order lẻ (Hàm này bạn đã có trong OrderRepository, nhớ kiểm tra vụ .ThenInclude nhé)
            var orders = await _unitOfWork.Orders.GetOrdersByUserIdAsync(userId, token);

            // Map sang OrderDto (Lúc này danh sách sẽ phẳng, dễ hiển thị)
            return _mapper.Map<List<OrderResponse>>(orders);
        }

        public async Task<List<OrderGroupResponse>> GetMyOrderGroupsAsync(Guid userId, CancellationToken token = default)
        {
            // Logic cũ giữ nguyên
            var groups = await _unitOfWork.OrderGroups.GetByUserIdAsync(userId);
            return _mapper.Map<List<OrderGroupResponse>>(groups);
        }

        public async Task<OrderGroupResponse> GetOrderGroupDetailAsync(Guid orderGroupId, CancellationToken token = default)
        {
            var group = await _unitOfWork.OrderGroups.GetByIdAsync(orderGroupId);
            if (group == null) throw new KeyNotFoundException("Order group not found.");
            return _mapper.Map<OrderGroupResponse>(group);
        }

        public async Task CancelOrderAsync(Guid userId, Guid orderId, string reason, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("Access denied.");

            if (order.OrderStatus != OrderStatus.Pending && order.PaymentStatus != PaymentStatus.Pending)
                throw new InvalidOperationException("Cannot cancel processed order.");

            // Refund Stock
            foreach (var item in order.OrderItems)
            {
                var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    //await _unitOfWork.Models.UpdateAsync(product);
                }
            }
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                if (order.OrderGroupId.HasValue)
                {
                    await _paymentService.RefundPaymentAsync(order.OrderGroupId.Value);
                    order.PaymentStatus = PaymentStatus.Refunded;
                    var group = await _unitOfWork.OrderGroups.GetByIdAsync(order.OrderGroupId.Value);
                    if (group != null) group.PaymentStatus = PaymentStatus.Refunded;
                }
            }

            order.OrderStatus = OrderStatus.Cancelled;
            order.CancelReason = reason;
            //await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // 3. SELLER / SHOP OWNER
        // =================================================================

        public async Task<List<OrderResponse>> GetShopOrdersAsync(Guid shopId, OrderStatus? status, int page, int size, CancellationToken token = default)
        {
            var orders = await _unitOfWork.Orders.GetShopOrdersAsync(shopId, status, page, size);
            return _mapper.Map<List<OrderResponse>>(orders);
        }

        public async Task<OrderResponse> GetShopOrderDetailAsync(Guid shopId, Guid orderId, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.ShopId != shopId) throw new UnauthorizedAccessException("This order does not belong to your shop.");

            return _mapper.Map<OrderResponse>(order);
        }

        public async Task UpdateOrderStatusAsync(Guid shopId, Guid orderId, OrderStatus newStatus, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.ShopId != shopId) throw new UnauthorizedAccessException("Access denied.");
            if (newStatus == OrderStatus.Cancelled && order.OrderStatus != OrderStatus.Cancelled)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        await _unitOfWork.Models.UpdateAsync(product);
                    }
                }
            }

            order.OrderStatus = newStatus;
            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PRIVATE HELPERS
        // =================================================================

        private async Task AddNormalItemToCartAsync(Order cartOrder, AddToCartRequest request, Model product)
        {
            var existingItem = cartOrder.OrderItems.FirstOrDefault(oi => oi.ProductId == product.Id && !oi.IsCustom);

            if (existingItem != null)
            {
                if (existingItem.Quantity + request.Quantity > product.StockQuantity)
                    throw new InvalidOperationException($"Insufficient stock.");

                existingItem.Quantity += request.Quantity;
                existingItem.UnitPrice = product.Price; // Update giá mới
                existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
            }
            else
            {
                cartOrder.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = cartOrder.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductImage = product.ThumbnailURL ?? "",
                    UnitPrice = product.Price,
                    Quantity = request.Quantity,
                    TotalPrice = product.Price * request.Quantity,
                    IsCustom = false
                });
            }
        }

        private async Task AddCustomItemToCartAsync(Order cartOrder, AddToCartRequest request, Model baseKit)
        {
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.BuilderSessionId.Value);
            if (session == null) throw new KeyNotFoundException("Session expired.");
            var selectedParts = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson);

            // Check trùng
            // check IsCustom và DesignConfig chứa SessionId
            var existingItem = cartOrder.OrderItems.FirstOrDefault(oi =>
                oi.IsCustom == true &&
                oi.DesignConfig != null &&
                oi.DesignConfig.Contains(session.Id.ToString()));

            if (existingItem != null)
            {
                // Nếu đã có -> Cộng thêm số lượng bàn phím
                existingItem.Quantity += request.Quantity;
                existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
            }
            else
            {
                // Nếu chưa -> Tạo mới
                var newItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = cartOrder.Id,
                    ProductId = baseKit.Id,
                    ProductName = $"{baseKit.Name} (Custom Build)",
                    ProductImage = baseKit.ThumbnailURL ?? "",
                    UnitPrice = baseKit.Price,
                    Quantity = request.Quantity,
                    IsCustom = true,
                    DesignConfig = JsonSerializer.Serialize(new { SessionId = session.Id }),
                    OrderItemComponents = new List<OrderItemComponent>()
                };

                //Loop qua từng linh kiện để tính tiền và số lượng
                if (selectedParts != null)
                {
                    foreach (var part in selectedParts.Values)
                    {
                        int qtyRecipe = part.Quantity > 0 ? part.Quantity : 1;
                        newItem.UnitPrice += (part.Price * qtyRecipe);
                        newItem.OrderItemComponents.Add(new OrderItemComponent
                        {
                            Id = Guid.NewGuid(),
                            OrderItemId = newItem.Id,
                            PartId = part.Id,
                            PartName = part.Name,
                            PartPriceSnapshot = part.Price,
                            PartImageUrl = part.ThumbnailUrl,
                            Quantity = qtyRecipe 
                        });
                    }
                }

                newItem.TotalPrice = newItem.UnitPrice * newItem.Quantity;
                cartOrder.OrderItems.Add(newItem);
            }
        }
    }
}