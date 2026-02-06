using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
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
        private readonly IVoucherService _voucherService;
        private readonly IWalletService _walletService;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper, IPaymentService paymentService, IVoucherService voucher, IWalletService wallet)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _paymentService = paymentService;
            _voucherService = voucher;
            _walletService = wallet;
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
        [Obsolete("This API is deprecated and disabled.")]
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


        public async Task<OrderIssueResponse> RequestCancelOrderAsync(Guid userId, DTOs.OrderIssues.CancelOrderRequest request, CancellationToken token = default)
        {
            // 1. Validate Order
            var order = await _unitOfWork.Orders.GetByIdAsync(request.OrderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("Not your order.");

            // Chặn nếu đơn hàng đã đi quá xa (Đã giao/Hoàn tất)
            if (order.OrderStatus == OrderStatus.Shipped || order.OrderStatus == OrderStatus.Completed)
            {
                throw new InvalidOperationException("Cannot cancel order at this stage.");
            }

            // 2. Check Spam: Hủy > 4 đơn/tuần (Tính cả AutoCancelled và ShopAccepted)
            var lastWeek = DateTime.UtcNow.AddDays(-7);

            // Đếm số đơn đã hủy thành công (Cancelled) của user này trong 7 ngày qua
            int cancelledCount = await _unitOfWork.OrderIssues.CountUserIssuesAsync(
                userId,
                OrderIssueStatus.AutoCancelled, // Hoặc status tương ứng khi hủy thành công
                lastWeek
            );

            bool isSpamRequest = cancelledCount >= 4;

            // 3. Tạo Entity OrderIssue
            var issue = new OrderIssue
            {
                OrderId = request.OrderId,
                UserId = userId,
                Type = OrderIssueType.CancelRequest, // Đổi tên Enum cho khớp code cũ của bạn
                Reason = request.Reason,
                Description = request.Description,
                // EvidenceUrl = request.EvidenceUrl, // Uncomment nếu DTO có trường này
                CreatedAt = DateTime.UtcNow,
                IsSystemValid = !isSpamRequest // Nếu spam thì invalid ngay từ đầu
            };

            // 4. Quyết định trạng thái ngay lập tức
            if (isSpamRequest)
            {
                issue.Status = OrderIssueStatus.Rejected;
                issue.ShopResponse = "System Auto-Reject: Spam limit reached (4 cancellations/week).";
                issue.AdminNote = "Auto-rejected by System.";
            }
            else
            {
                // Valid -> Chờ Shop xử lý
                issue.Status = OrderIssueStatus.InProgress;
            }

            // 5. Lưu vào DB
            await _unitOfWork.OrderIssues.AddAsync(issue);

            // Tạo Log khởi tạo
            var log = new OrderIssueLog
            {
                OrderIssueId = issue.Id,
                ActionById = userId,
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.Create,
                Comment = "Customer requested cancellation."
            };
            await _unitOfWork.OrderIssueLogs.AddAsync(log);

            await _unitOfWork.CommitAsync();

            return _mapper.Map<OrderIssueResponse>(issue);
        }

        public async Task ProcessCancelRequestAsync(Guid actorId, ProcessIssueRequest request, CancellationToken token = default)
        {
            // 1. Lấy Issue
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(request.IssueId);
            if (issue == null) throw new KeyNotFoundException("Order issue not found");

            // 2. Lấy Order
            var order = await _unitOfWork.Orders.GetByIdAsync(issue.OrderId);
            if (order == null) throw new KeyNotFoundException("Related order not found");

            // [FIX 1] Đảm bảo Shop luôn được load. Nếu Repo chưa include thì load thủ công.
            if (order.Shop == null && order.ShopId.HasValue)
            {
                order.Shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
            }

            // 3. XÁC ĐỊNH NGƯỜI THỰC HIỆN (REAL ACTOR)
            Guid realActionUserId = actorId;

            // Trường hợp A: Hệ thống/Worker gọi (truyền Guid.Empty)
            if (realActionUserId == Guid.Empty)
            {
                if (order.Shop != null)
                {
                    realActionUserId = order.Shop.UserId; // Hệ thống đóng vai chủ shop
                }
                else
                {
                    // Nếu đơn không có Shop (lỗi data), log warning và tiếp tục (hoặc throw)
                    // Ở đây gán tạm là CustomerId hoặc 1 ID hệ thống để không crash luồng Refund
                    throw new InvalidOperationException("System cannot identify Shop Owner (Shop data missing).");
                }

                if (string.IsNullOrEmpty(request.ShopResponse))
                    request.ShopResponse = "System Auto-Process: Request timeout.";
            }
            // Trường hợp B: Shop Owner (hoặc User) gọi API thủ công
            else
            {
                // [FIX 2] Validate Quyền chặt chẽ & Thông báo rõ ràng
                if (order.Shop != null)
                {
                    // Nếu người gọi KHÁC chủ shop
                    if (order.Shop.UserId != realActionUserId)
                    {
                        // Mở comment dòng dưới nếu muốn cho phép ADMIN xử lý (cần check Role)
                        // var user = await _unitOfWork.Users.GetByIdAsync(realActionUserId);
                        // if (user.Role.Name != RoleType.Admin) 

                        throw new UnauthorizedAccessException($"Access Denied");
                    }
                }
                else if (order.ShopId.HasValue)
                {
                    // ShopId có value mà Shop object vẫn null -> Lỗi DB/Query
                    throw new KeyNotFoundException($"ShopProfile with ID {order.ShopId} not found.");
                }
            }

            if (issue.Status == request.Decision)
            {
                return; // Đã xử lý rồi, không làm gì thêm
            }

            // Validate trạng thái 
            if (issue.Status != OrderIssueStatus.InProgress && issue.Status != OrderIssueStatus.Pending)
                throw new InvalidOperationException($"This request has already been processed (Current Status: {issue.Status}).");

            // Cập nhật Issue
            issue.ShopResponse = request.ShopResponse;
            issue.UpdatedAt = DateTime.UtcNow;

            // 4. Xử lý theo quyết định
            switch (request.Decision)
            {
                case OrderIssueStatus.ShopAccepted:
                case OrderIssueStatus.AutoCancelled:
                    var newStatus = request.Decision == OrderIssueStatus.AutoCancelled
                                    ? OrderIssueStatus.AutoCancelled
                                    : OrderIssueStatus.ShopAccepted;

                    issue.Status = newStatus;

                    // Hủy đơn
                    order.OrderStatus = OrderStatus.Cancelled;
                    order.CancelReason = request.Decision == OrderIssueStatus.AutoCancelled
                                         ? "Request timeout (Auto-Refund)"
                                         : $"Shop approved: {issue.Reason}";

                    // === LOGIC HOÀN TIỀN / TRẢ KHO ===
                    // 4.1 Trả hàng về kho
                    // Lưu ý: Cần load OrderItems nếu chưa có
                    if (order.OrderItems == null || !order.OrderItems.Any())
                    {
                        // Load lại order với items nếu cần thiết (thường Repo GetById đã có)
                        // var fullOrder = ... 
                    }

                    if (order.OrderItems != null)
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

                    // 4.2 Tạo Voucher đền bù (Nếu đã thanh toán)
                    if (order.PaymentStatus == PaymentStatus.Paid)
                    {
                        // Dùng realActionUserId (Chủ Shop) làm CreatorId
                        await _voucherService.CreateCompensationVoucherAsync(
                            realActionUserId,
                            issue.UserId,
                            order.TotalAmount
                        );

                        // TODO: Gọi WalletService trừ tiền shop tại đây (nếu có)

                        order.PaymentStatus = PaymentStatus.Refunded;
                    }
                    break;

                case OrderIssueStatus.Rejected:
                    issue.Status = OrderIssueStatus.Rejected;
                    break;

                default:
                    throw new ArgumentException("Invalid decision status.");
            }

            // 5. Save Order Updates
            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                await _unitOfWork.Orders.UpdateOrderAsync(order);
            }

            // 6. GHI LOG
            var log = new OrderIssueLog
            {
                Id = Guid.NewGuid(),
                OrderIssueId = issue.Id,
                ActionById = realActionUserId,
                ActionByRole = RoleType.Shop,
                Action = request.Decision == OrderIssueStatus.Rejected ? OrderIssueAction.ShopReject : OrderIssueAction.ShopApprove,
                Comment = request.ShopResponse ?? "Processed cancellation request.",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.OrderIssueLogs.AddAsync(log);

            // Update Issue & Commit
            _unitOfWork.OrderIssues.Update(issue);
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

        private async Task ExecuteRefundStrategyAsync(Order order)
        {
            // A. Hoàn trả tồn kho (Stock)
            // Cần load OrderItems nếu chưa có
            // Lưu ý: Nếu OrderItems chưa được Include trong GetByIdAsync ở trên thì phải load lại hoặc Include ngay từ đầu
            // Giả sử repo đã include OrderItems
            foreach (var item in order.OrderItems)
            {
                var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    await _unitOfWork.Models.UpdateAsync(product);
                }
            }

            // B. Xử lý tiền (Voucher/Refund)
            // Nếu chưa thanh toán -> ko cần làm gì
            if (order.PaymentStatus == PaymentStatus.Pending || order.PaymentStatus == PaymentStatus.Pending)
            {
                order.PaymentStatus = PaymentStatus.Failed;
                return;
            }

            // Nếu đã thanh toán -> Tạo Voucher (TODO)
            /* // TODO: Voucher Logic
               var voucher = new Voucher { ... };
               await _unitOfWork.Vouchers.AddAsync(voucher);
            */

            // Update trạng thái tiền
            order.PaymentStatus = PaymentStatus.Refunded;
        }
    }
}