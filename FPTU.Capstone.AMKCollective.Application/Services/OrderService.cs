using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;
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
        private readonly OrderSettings _orderSettings;
        private readonly FrontendUrls _frontendUrls;
        private readonly SystemSettings _systemSettings;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper, IPaymentService paymentService, IVoucherService voucher, IWalletService wallet, IOptions<OrderSettings> orderOptions,
        IOptions<FrontendUrls> urlOptions, IOptions<SystemSettings> systemSettings)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _paymentService = paymentService;
            _voucherService = voucher;
            _walletService = wallet;
            _orderSettings = orderOptions.Value;
            _frontendUrls = urlOptions.Value;
            _systemSettings = systemSettings.Value;
        }

        // =================================================================
        // 1. SHOPPING CART (ADD, GET, REMOVE, UPDATE)
        // =================================================================

        public async Task AddToCartAsync(Guid userId, AddToCartRequest request, CancellationToken token = default)
        {
            if (request.Quantity <= 0) request.Quantity = 1;

            // ══════════════════════════════════════════════════════════════
            // PHASE 1: READ-ONLY — Thu thập dữ liệu cần thiết, snapshot vào biến cục bộ.
            // ══════════════════════════════════════════════════════════════

            Guid? productId = null;
            Guid? assembledProductId = null;
            string productName;
            string productImage;
            decimal productPrice;
            int productStock;
            bool productIsActive;
            bool shopUnavailable = false;

            // Dữ liệu builder session (chỉ dùng khi custom)
            string? sessionCurrentStep = null;
            Guid? sessionBaseKitId = null;
            string? sessionSelectedItemsJson = null;
            Guid? sessionId = null;

            if (request.BuilderSessionId.HasValue)
            {
                var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.BuilderSessionId.Value);
                if (session == null) throw new KeyNotFoundException("Builder session not found.");

                sessionId = session.Id;
                sessionCurrentStep = session.CurrentStep;
                sessionBaseKitId = session.BaseKitId;
                sessionSelectedItemsJson = session.SelectedItemsJson;

                var baseKit = await _unitOfWork.Models.GetByIdAsync(session.BaseKitId);
                if (baseKit == null) throw new KeyNotFoundException("Product not found.");

                productId = baseKit.Id;
                productName = baseKit.Name;
                productImage = baseKit.ThumbnailURL ?? "";
                productPrice = baseKit.Price;
                productStock = baseKit.StockQuantity;
                productIsActive = baseKit.IsActive;
                if (baseKit.Shop != null)
                    shopUnavailable = baseKit.Shop.Status != ShopStatus.Active || !baseKit.Shop.IsActive;
            }
            else if (!request.IsCustom)
            {
                if (request.ProductId == null) throw new ArgumentNullException(nameof(request.ProductId));

                var snapshot = await FetchAssembledProductSnapshotAsync(request.ProductId.Value);

                assembledProductId = snapshot.Id;
                productName = snapshot.Name;
                productImage = snapshot.Image;
                productPrice = snapshot.Price;
                productStock = snapshot.Stock;
                productIsActive = snapshot.IsActive;
                shopUnavailable = snapshot.ShopUnavailable;
            }
            else
            {
                throw new InvalidOperationException("Invalid cart request parameters.");
            }

            if (!productIsActive) throw new InvalidOperationException("Product is inactive.");
            if (shopUnavailable) throw new InvalidOperationException("Shop unavailable.");
            if (productStock < request.Quantity)
                throw new InvalidOperationException($"Insufficient stock. Available: {productStock}");

            // ══════════════════════════════════════════════════════════════
            // PHASE 2: WRITE 
            // ══════════════════════════════════════════════════════════════
            _unitOfWork.ClearChangeTracker();
            var cartOrder = await _unitOfWork.Orders.GetCartOnlyAsync(userId);

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

                if (request.BuilderSessionId.HasValue)
                {
                    if (sessionCurrentStep != "complete")
                        throw new InvalidOperationException($"Builder session chưa hoàn tất. Bước hiện tại: '{sessionCurrentStep}'.");
                    var selectedParts = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(sessionSelectedItemsJson!);
                    cartOrder.OrderItems.Add(BuildCustomOrderItem(cartOrder.Id, request.Quantity, productId!.Value, productName, productImage, productPrice, sessionId!.Value, selectedParts));
                }
                else
                {
                    cartOrder.OrderItems.Add(BuildNormalOrderItem(cartOrder.Id, request.Quantity, assembledProductId!.Value, productName, productImage, productPrice));
                }

                cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
                cartOrder.SubTotal = cartOrder.TotalAmount;
                await _unitOfWork.Orders.AddAsync(cartOrder);
                await _unitOfWork.CommitAsync();
            }
            else
            {
                OrderItem? itemToInsert = null;

                if (request.BuilderSessionId.HasValue)
                {
                    // (Logic Builder Giữ Nguyên - Không cần thay đổi)
                    if (sessionCurrentStep != "complete")
                        throw new InvalidOperationException($"Builder session chưa hoàn tất. Bước hiện tại: '{sessionCurrentStep}'.");
                    var selectedParts = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(sessionSelectedItemsJson!);
                    var sessionIdStr = sessionId!.Value.ToString();

                    var existingItem = cartOrder.OrderItems.FirstOrDefault(x =>
                        x.IsCustom && !x.IsDeleted && x.DesignConfig != null && x.DesignConfig.Contains(sessionIdStr));

                    if (existingItem != null)
                    {
                        int newQty = existingItem.Quantity + request.Quantity;
                        decimal newTotal = existingItem.UnitPrice * newQty;
                        existingItem.Quantity = newQty;
                        existingItem.TotalPrice = newTotal;
                        _unitOfWork.Orders.UpdateItemQuantity(existingItem.Id, newQty, existingItem.UnitPrice, newTotal);
                    }
                    else
                    {
                        itemToInsert = BuildCustomOrderItem(cartOrder.Id, request.Quantity, productId!.Value, productName, productImage, productPrice, sessionId!.Value, selectedParts);
                        cartOrder.OrderItems.Add(itemToInsert);
                    }
                }
                else
                {
                    // Lọc trùng item AssembledProduct
                    var existingItem = cartOrder.OrderItems.FirstOrDefault(oi => oi.AssembledProductId == assembledProductId && !oi.IsCustom && !oi.IsDeleted);

                    if (existingItem != null)
                    {
                        int newQty = existingItem.Quantity + request.Quantity;
                        if (newQty > productStock)
                            throw new InvalidOperationException($"Insufficient stock.");
                        decimal newTotal = newQty * productPrice;
                        existingItem.Quantity = newQty;
                        existingItem.UnitPrice = productPrice;
                        existingItem.TotalPrice = newTotal;
                        _unitOfWork.Orders.UpdateItemQuantity(existingItem.Id, newQty, productPrice, newTotal);
                    }
                    else
                    {
                        itemToInsert = BuildNormalOrderItem(cartOrder.Id, request.Quantity, assembledProductId!.Value, productName, productImage, productPrice);
                        cartOrder.OrderItems.Add(itemToInsert);
                    }
                }

                if (itemToInsert != null)
                    await _unitOfWork.Orders.AddOrderItemAsync(itemToInsert);

                decimal cartTotal = cartOrder.OrderItems.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);
                _unitOfWork.Orders.UpdateCartTotal(cartOrder.Id, cartTotal);

                await _unitOfWork.CommitAsync();

                _unitOfWork.ClearChangeTracker();

                var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(cartOrder.Id);

                if (appliedVouchers != null && appliedVouchers.Any())
                {
                    await _voucherService.RemoveAllVouchersAsync(userId, cartOrder.Id);
                }
            }
        }

        public async Task<OrderResponse> GetMyCartAsync(Guid userId, CancellationToken token = default)
        {
            // 1. Lấy dữ liệu Giỏ hàng từ DB (Đã bao gồm OrderItems và Components của Custom)
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);

            // Nếu chưa có giỏ hàng, trả về null hoặc object rỗng tùy convention
            if (cartOrder == null) return null;

            // 2. Map sang DTO trước để thao tác trên dữ liệu trả về (không sửa trực tiếp vào Entity đang tracking)
            var result = _mapper.Map<OrderResponse>(cartOrder);

            // Biến cờ để đánh dấu xem giỏ hàng có vấn đề gì không (nếu cần hiển thị alert tổng)
            bool hasStockIssue = false;
            bool isPriceChanged = false;

            // 3. Duyệt qua từng sản phẩm trong giỏ để Validate Real-time
            foreach (var itemDto in result.OrderItems)
            {
                var validationResult = await ValidateCartItemRealtimeAsync(itemDto, cartOrder);

                if (validationResult.hasStockIssue) hasStockIssue = true;
                if (validationResult.isPriceChanged) isPriceChanged = true;
            }


            // 4. Tính lại tổng tiền giỏ hàng (Sau khi đã update giá các item)
            result.SubTotal = result.OrderItems.Sum(i => i.TotalPrice);
            result.DiscountAmount = cartOrder.DiscountAmount;
            result.TotalAmount = Math.Max(0, result.SubTotal + result.ShippingFee - result.DiscountAmount);

            if (isPriceChanged || cartOrder.SubTotal != result.SubTotal)
            {
                cartOrder.SubTotal = result.SubTotal;
                cartOrder.TotalAmount = result.TotalAmount;
                cartOrder.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.Orders.UpdateOrderAsync(cartOrder, token);
                await _unitOfWork.CommitAsync();
            }

            return result;
            
        }

        public async Task RemoveItemFromCartAsync(Guid userId, Guid orderItemId, CancellationToken token = default)
        {
            // 1. Lấy giỏ hàng
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null) throw new KeyNotFoundException("Cart is empty.");

            // 2. Tìm item cần xóa
            var item = cartOrder.OrderItems.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null) throw new KeyNotFoundException("Item not found in cart.");

            // 3. THỰC HIỆN HARD DELETE
            _unitOfWork.Orders.DeleteOrderItem(item);

            // Đồng thời xóa khỏi list trong bộ nhớ để tính lại tiền cho đúng ngay lập tức
            cartOrder.OrderItems.Remove(item);

            // 4. Tính lại tổng tiền
            cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
            cartOrder.SubTotal = cartOrder.TotalAmount;

            var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(cartOrder.Id); // cartOrder là biến lưu order giỏ hàng hiện tại của bạn

            if (appliedVouchers != null && appliedVouchers.Any())
            {
                // Nếu có, lập tức gỡ bỏ toàn bộ voucher để tránh sai lệch tính toán.
                await _voucherService.RemoveAllVouchersAsync(userId, cartOrder.Id);
            }
            // 5. Lưu thay đổi
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

            // ==========================================
            // LOGIC TÍNH LẠI GIÁ & CHECK KHO TỒN
            // ==========================================
            if (item.IsCustom)
            {
                await ProcessCustomItemUpdateAsync(item, newQuantity);
            }
            else
            {
                await ProcessAssembledItemUpdateAsync(item, newQuantity);
            }

            // ==========================================
            // CẬP NHẬT TỔNG TIỀN VÀ XỬ LÝ VOUCHER
            // ==========================================
            cartOrder.TotalAmount = cartOrder.OrderItems.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);
            cartOrder.SubTotal = cartOrder.TotalAmount;

            var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(cartOrder.Id);

            if (appliedVouchers != null && appliedVouchers.Any())
            {
                // Gỡ bỏ toàn bộ voucher để tránh sai lệch tính toán
                await _voucherService.RemoveAllVouchersAsync(userId, cartOrder.Id);
            }

            await _unitOfWork.CommitAsync();
        }

        public async Task<CalculateCartResponse> CalculateCartPreviewAsync(Guid userId, CalculateCartRequest request)
        {
            var response = new CalculateCartResponse();

            // 1. Get current cart
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null || !cartOrder.OrderItems.Any())
                return response;

            // 2. FILTER ONLY TICKED ITEMS FROM FRONTEND
            var selectedItems = cartOrder.OrderItems
                .Where(i => !i.IsDeleted && request.SelectedOrderItemIds.Contains(i.Id))
                .ToList();

            if (!selectedItems.Any())
                return response;

            // 3. Group items by Shop
            var shopItemsDict = new Dictionary<Guid, List<OrderItem>>();
            foreach (var item in selectedItems)
            {
                // Tự động phân luồng lấy ShopId dựa vào IsCustom
                Guid shopId = await GetShopIdForCartItemAsync(item);

                if (shopId != Guid.Empty)
                {
                    if (!shopItemsDict.ContainsKey(shopId))
                        shopItemsDict[shopId] = new List<OrderItem>();
                    shopItemsDict[shopId].Add(item);
                }
            }

            // 4. Calculate per Shop
            decimal totalCartSubTotal = 0;
            decimal totalShippingFee = 0;
            decimal totalShopDiscount = 0;

            foreach (var kvp in shopItemsDict)
            {
                var shopId = kvp.Key;
                var items = kvp.Value;
                var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);

                // Calculate subtotal only for TICKED items
                decimal shopSubTotal = items.Sum(i => i.TotalPrice);

                // Lấy phí ship từ appsettings.json 
                decimal shopShippingFee = _orderSettings.DefaultShippingFee;

                decimal shopDiscount = 0;
                string? shopVoucherError = null;

                // Validate Shop Voucher if provided
                if (request.AppliedShopVoucherCodes.TryGetValue(shopId, out var shopVoucherCode) && !string.IsNullOrEmpty(shopVoucherCode))
                {
                    var shopVoucher = await _unitOfWork.Vouchers.GetByCodeAsync(shopVoucherCode);
                    if (shopVoucher == null)
                    {
                        shopVoucherError = "Voucher does not exist.";
                    }
                    else if (shopVoucher.Status != VoucherStatus.Active || shopVoucher.StartDate > DateTime.UtcNow || shopVoucher.EndDate < DateTime.UtcNow)
                    {
                        shopVoucherError = "Voucher is expired or not yet active.";
                    }
                    else if (shopVoucher.UsedCount >= shopVoucher.UsageLimit)
                    {
                        shopVoucherError = "Voucher usage limit reached.";
                    }
                    else if (shopVoucher.CreatorId != shop?.UserId && shopVoucher.Type != VoucherType.Compensation)
                    {
                        shopVoucherError = "Voucher is not applicable for this shop.";
                    }
                    else if (shopSubTotal < shopVoucher.MinOrderValue)
                    {
                        shopVoucherError = $"Minimum order value of {shopVoucher.MinOrderValue:N0} VND not met.";
                    }
                    else
                    {
                        // Valid voucher -> Calculate discount
                        if (shopVoucher.DiscountType == DiscountType.FixedAmount)
                        {
                            shopDiscount = shopVoucher.Value;
                        }
                        else // Percentage
                        {
                            shopDiscount = shopSubTotal * (shopVoucher.Value / 100);
                            if (shopVoucher.MaxDiscountAmount.HasValue && shopDiscount > shopVoucher.MaxDiscountAmount.Value)
                                shopDiscount = shopVoucher.MaxDiscountAmount.Value;
                        }
                        // Cap: Discount cannot exceed shop subtotal
                        if (shopDiscount > shopSubTotal) shopDiscount = shopSubTotal;
                    }
                }

                var preview = new ShopCartPreviewDto
                {
                    ShopId = shopId,
                    ShopName = shop?.ShopName ?? "Shop",
                    SubTotal = shopSubTotal,
                    ShippingFee = shopShippingFee,
                    ShopDiscountAmount = shopDiscount,
                    TotalAmount = Math.Max(0, shopSubTotal + shopShippingFee - shopDiscount),
                    IncludedOrderItemIds = items.Select(i => i.Id).ToList(),
                    ShopVoucherError = shopVoucherError
                };

                response.ShopPreviews.Add(preview);

                totalCartSubTotal += shopSubTotal;
                totalShippingFee += shopShippingFee;
                totalShopDiscount += shopDiscount;
            }

            // 5. Calculate System Voucher if provided
            decimal systemDiscount = 0;
            string? systemVoucherError = null;

            if (!string.IsNullOrEmpty(request.AppliedSystemVoucherCode))
            {
                var sysVoucher = await _unitOfWork.Vouchers.GetByCodeAsync(request.AppliedSystemVoucherCode);
                if (sysVoucher == null)
                {
                    systemVoucherError = "System voucher does not exist.";
                }
                else if (sysVoucher.Scope != VoucherScope.System && sysVoucher.Type != VoucherType.Compensation)
                {
                    systemVoucherError = "This is not a system voucher.";
                }
                else if (sysVoucher.Status != VoucherStatus.Active || sysVoucher.StartDate > DateTime.UtcNow || sysVoucher.EndDate < DateTime.UtcNow)
                {
                    systemVoucherError = "System voucher is expired or not yet active.";
                }
                else if (sysVoucher.UsedCount >= sysVoucher.UsageLimit)
                {
                    systemVoucherError = "System voucher usage limit reached.";
                }
                else if (totalCartSubTotal < sysVoucher.MinOrderValue)
                {
                    systemVoucherError = $"Minimum order value of {sysVoucher.MinOrderValue:N0} VND not met.";
                }
                else
                {
                    if (sysVoucher.DiscountType == DiscountType.FixedAmount)
                    {
                        systemDiscount = sysVoucher.Value;
                    }
                    else
                    {
                        systemDiscount = totalCartSubTotal * (sysVoucher.Value / 100);
                        if (sysVoucher.MaxDiscountAmount.HasValue && systemDiscount > sysVoucher.MaxDiscountAmount.Value)
                            systemDiscount = sysVoucher.MaxDiscountAmount.Value;
                    }
                }
            }

            // Cap: System discount cannot exceed the remaining subtotal after shop discounts
            decimal remainingSubTotal = totalCartSubTotal - totalShopDiscount;
            if (systemDiscount > remainingSubTotal) systemDiscount = remainingSubTotal;

            // 6. Assemble final response
            response.TotalCartSubTotal = totalCartSubTotal;
            response.TotalShippingFee = totalShippingFee;
            response.TotalDiscountAmount = totalShopDiscount + systemDiscount;
            response.FinalTotalAmount = Math.Max(0, totalCartSubTotal + totalShippingFee - response.TotalDiscountAmount);
            response.SystemVoucherError = systemVoucherError;

            return response;
        }

        // =================================================================
        // 2. CHECKOUT & CUSTOMER ORDERS
        // =================================================================

        public async Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default)
        {
            // 1. Kiểm tra giỏ hàng và lấy danh sách sản phẩm được chọn
            var (cartOrder, selectedItems) = await ValidateAndGetCartItemsAsync(userId, request.SelectedOrderItemIds);

            // 2. Kiểm tra và lấy danh sách Voucher hợp lệ
            var activeVouchers = await ValidateAndGetActiveVouchersAsync(cartOrder.Id);

            // Chuẩn bị URL thanh toán
            string successUrl = string.IsNullOrEmpty(request.SuccessUrl) ? _frontendUrls.PaymentSuccessPath : request.SuccessUrl;
            string cancelUrl = string.IsNullOrEmpty(request.CancelUrl) ? _frontendUrls.PaymentCancelPath : request.CancelUrl;

            // 3. Thực thi logic lõi trong Transaction
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    // Khởi tạo Order Group
                    var orderGroup = new OrderGroup
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = userId,
                        PaymentStatus = PaymentStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        TotalGroupAmount = 0,
                        Orders = new List<Order>()
                    };

                    // Bước 3.1: Chia đơn theo Shop, trừ kho và tính Subtotal
                    decimal totalCheckoutSubTotal = await CreateOrdersAndDeductStockAsync(userId, request, selectedItems, orderGroup);

                    // Bước 3.2: Phân bổ Voucher và chốt tổng tiền thanh toán
                    var appliedVoucherIds = await ApplyVouchersAndCalculateTotalsAsync(userId, orderGroup, activeVouchers, totalCheckoutSubTotal);

                    // Bước 3.3: Lưu OrderGroup
                    await _unitOfWork.OrderGroups.CreateAsync(orderGroup);

                    // Bước 3.4: Tăng lượt dùng voucher và dọn dẹp giỏ hàng
                    await FinalizeVouchersAndCleanupCartAsync(cartOrder, selectedItems, appliedVoucherIds);

                    await _unitOfWork.CommitAsync();

                    // Bước 4: Xử lý rẽ nhánh thanh toán (Ví / Cổng thanh toán)
                    return await ProcessPaymentBranchAsync(userId, request.PaymentMethod, orderGroup, successUrl, cancelUrl, token);
                }
                catch (Exception ex)
                {
                    // Quăng lỗi ra để ExecutionStrategy bắt và TỰ ĐỘNG gọi RollbackTransactionAsync
                    throw;
                }
            });
        }

        public async Task<List<OrderResponse>> GetMyOrdersAsync(Guid userId, CancellationToken token = default)
        {
            // Gọi Repo lấy Order lẻ (Hàm này bạn đã có trong OrderRepository, nhớ kiểm tra vụ .ThenInclude nhé)
            var orders = await _unitOfWork.Orders.GetOrdersByUserIdAsync(userId, false, token);
            var historyOrders = orders.Where(o => o.OrderStatus != OrderStatus.InCart).ToList();
            // Map sang OrderDto (Lúc này danh sách sẽ phẳng, dễ hiển thị)
            return _mapper.Map<List<OrderResponse>>(historyOrders);
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
                await RefundItemStockAsync(item);
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
            if (order == null) throw new KeyNotFoundException("Order not found.");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("Not your order.");

            // Chặn nếu đơn hàng đã giao hoặc hoàn tất
            if (order.OrderStatus == OrderStatus.Shipped || order.OrderStatus == OrderStatus.Completed)
            {
                throw new InvalidOperationException("Cannot cancel order at this stage.");
            }

            // 2. [Fix #2] Chặn duplicate: đơn đã có issue InProgress/Pending rồi
            bool hasActiveIssue = await _unitOfWork.OrderIssues.HasActiveIssueForOrderAsync(request.OrderId);
            if (hasActiveIssue)
                throw new InvalidOperationException("A cancellation request for this order is already in progress. Please wait for it to be processed.");

            // 3. [Fix #1] Spam check chuẩn e-commerce: đếm tất cả request hủy đã xử lý (Accepted, AutoCancelled) + đang chờ (InProgress)
            // Không tính Rejected vì đấy là đơn không bị hủy
            var lastPeriod = DateTime.UtcNow.AddDays(-_orderSettings.CancellationSpamCheckDays);
            var spamStatuses = new[]
            {
                OrderIssueStatus.ShopAccepted,
                OrderIssueStatus.AutoCancelled,
                OrderIssueStatus.InProgress
            };
            int cancelAttempts = await _unitOfWork.OrderIssues.CountUserCancelAttemptsAsync(userId, spamStatuses, lastPeriod);
            bool isSpamRequest = cancelAttempts >= _orderSettings.MaxCancellationsPerPeriod;

            // 3. Create Entity OrderIssue
            var issue = new OrderIssue
            {
                OrderId = request.OrderId,
                UserId = userId,
                Type = OrderIssueType.CancelRequest,
                Reason = request.Reason,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                IsSystemValid = !isSpamRequest
            };

            // 4. Decide Initial Status
            if (isSpamRequest)
            {
                issue.Status = OrderIssueStatus.Rejected;
                issue.ShopResponse = "System Auto-Reject: Spam limit reached (4 cancellations/week).";
                issue.AdminNote = "Auto-rejected by System.";
            }
            else
            {
                // Theo BR: Status mặc định là Pending. Sau khi valid thì đổi thành InProgress để báo Shop
                issue.Status = OrderIssueStatus.InProgress;
            }

            // 5. Save to DB
            await _unitOfWork.OrderIssues.AddAsync(issue);

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

            // TODO: notify shop

            return _mapper.Map<OrderIssueResponse>(issue);
        }

        public async Task ProcessCancelRequestAsync(Guid actorId, ProcessIssueRequest request, CancellationToken token = default)
        {
            // 1. Get Issue & Order
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(request.IssueId);
            if (issue == null) throw new KeyNotFoundException("Order issue not found");

            var order = await _unitOfWork.Orders.GetByIdAsync(issue.OrderId);
            if (order == null) throw new KeyNotFoundException("Related order not found");

            if (order.Shop == null && order.ShopId.HasValue)
            {
                order.Shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
            }

            // 2. IDENTIFY THE ACTOR (System or Shop Owner)
            Guid realActionUserId = actorId;

            // Trường hợp A: Hệ thống Auto-Cancel (truyền Guid.Empty từ Worker sau 24h)
            if (realActionUserId == Guid.Empty)
            {
                if (order.Shop != null)
                {
                    realActionUserId = order.Shop.UserId; // Hệ thống lấy danh nghĩa Shop để chịu phí
                }
                else
                {
                    throw new InvalidOperationException("System cannot identify Shop Owner.");
                }

                if (string.IsNullOrEmpty(request.ShopResponse))
                    request.ShopResponse = "System Auto-Process: Request timeout (24h). Auto accepted.";
            }
            // Trường hợp B: Shop xử lý thủ công
            else
            {
                if (order.Shop != null && order.Shop.UserId != realActionUserId)
                {
                    throw new UnauthorizedAccessException("Access Denied. Only Shop Owner can process this.");
                }
            }

            if (issue.Status == request.Decision) return; // Đã xử lý

            if (issue.Status != OrderIssueStatus.InProgress && issue.Status != OrderIssueStatus.Pending)
                throw new InvalidOperationException($"This request has already been processed (Current Status: {issue.Status}).");

            issue.ShopResponse = request.ShopResponse;
            issue.UpdatedAt = DateTime.UtcNow;

            // 3. PROCESS DECISION
            switch (request.Decision)
            {
                case OrderIssueStatus.ShopAccepted:
                case OrderIssueStatus.AutoCancelled:

                    issue.Status = request.Decision;

                    // [Fix #5] Snapshot trạng thái TRƯỚC khi đổi sang Cancelled
                    bool isCompleted = order.OrderStatus == OrderStatus.Completed;
                    bool isOrderPaid = order.PaymentStatus == PaymentStatus.Paid;

                    order.OrderStatus = OrderStatus.Cancelled;
                    order.CancelReason = request.Decision == OrderIssueStatus.AutoCancelled
                                         ? "Request timeout 24h (Auto-Refund)"
                                         : $"Shop approved: {issue.Reason}";

                    // --- 3.1 TRẢ HÀNG VỀ KHO ---
                    if (order.OrderItems != null)
                    {
                        foreach (var item in order.OrderItems)
                        {
                            await RefundItemStockAsync(item);
                        }
                    }

                    // --- 3.2 XỬ LÝ VOUCHER & TẠO VOUCHER REFUND (BAO GỒM PHẠT SHOP) ---
                    // Chắc chắn đơn đã Paid nên không cần check if (PaymentStatus == Paid) nữa, 
                    // nhưng muốn an toàn thì vẫn giữ.
                    if (isOrderPaid)
                    {
                        decimal cashPaidAmount = order.TotalAmount;

                        // BƯỚC 3.2.1: Hoàn 100% tiền thật vào Ví Khách Hàng
                        await _walletService.RefundToWalletAsync(
                            issue.UserId,
                            cashPaidAmount,
                            $"Refund for cancelled order #{order.Id}"
                        );

                        // BƯỚC 3.2.2: Trừ tiền hàng khỏi Ví (HeldBalance) của Shop
                        // Vì Shop không giao hàng nên phải rút lại tiền doanh thu đang tạm giữ
                        await _walletService.DeductFundsForRefundAsync(
                            realActionUserId,
                            order.Id,
                            cashPaidAmount,
                            isCompleted
                        );

                        // BƯỚC 3.2.3: Hoàn lượt dùng Voucher gốc
                        var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(order.Id);
                        foreach (var av in appliedVouchers)
                        {
                            var appliedVoucher = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                            // Trả lại lượt dùng cho mọi loại voucher để khách tự xài lại ở đơn sau
                            if (appliedVoucher != null && appliedVoucher.UsedCount > 0 && DateTime.UtcNow <= appliedVoucher.EndDate)
                            {
                                appliedVoucher.UsedCount--;
                                _unitOfWork.Vouchers.Update(appliedVoucher);
                            }
                        }

                        // BƯỚC 3.2.4: Phân định lỗi & Xử phạt
                        bool isShopFault = request.Decision == OrderIssueStatus.AutoCancelled;

                        if (isShopFault)
                        {
                            // Tính tiền phạt Shop
                            decimal penaltyRate = _orderSettings.ShopCancellationPenaltyRate;
                            decimal penaltyAmount = cashPaidAmount * penaltyRate;

                            if (penaltyAmount > 0)
                            {
                                // A. Trừ tiền phạt vào Ví của Shop
                                await _walletService.AdjustBalanceAsync(
                                    realActionUserId,
                                     new AdjustBalanceRequest
                                     {
                                         UserId = realActionUserId,
                                         Amount = -penaltyAmount,
                                         Reason = $"Penalty fee for 24h timeout auto-cancel Order #{order.Id}"
                                     }
                                );

                                // B. Tặng Voucher Đền bù cho khách (Do System Bot tạo)
                                Guid systemBotId = _systemSettings.SystemBotId;

                                await _voucherService.CreateCompensationVoucherAsync(
                                    systemBotId,
                                    issue.UserId,
                                    penaltyAmount // Mệnh giá đúng bằng tiền phạt của Shop
                                );
                            }
                        }

                        order.PaymentStatus = PaymentStatus.Refunded;
                    }
                    break;

                case OrderIssueStatus.Rejected:
                    issue.Status = OrderIssueStatus.Rejected;
                    // Shop từ chối hủy -> Đơn hàng vẫn tiếp tục, không hoàn tiền.
                    break;

                default:
                    throw new ArgumentException("Invalid decision status.");
            }

            // 4. Save Updates
            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                await _unitOfWork.Orders.UpdateOrderAsync(order);
            }

            // 5. Create Log
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

            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();

            // TODO: notify User
        }

        public async Task<CheckoutResponse> RepayAsync(Guid userId, RepayRequest request, CancellationToken token = default)
        {
            // 1. Lấy thông tin đơn hàng (Kèm theo các order con bên trong)
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(request.OrderGroupId);

            if (orderGroup == null)
                throw new KeyNotFoundException("Order not found.");

            // 2. Validate quyền sở hữu
            if (orderGroup.CustomerId != userId)
                throw new UnauthorizedAccessException("You can only pay for your own orders.");

            // 3. Validate trạng thái
            if (orderGroup.PaymentStatus == PaymentStatus.Paid)
                throw new InvalidOperationException("This order has already been paid.");

            if (orderGroup.Orders.Any(o => o.OrderStatus == OrderStatus.Cancelled))
                throw new InvalidOperationException("This order has been cancelled. Please order again.");

            // Chuẩn bị URL
            string successUrl = string.IsNullOrEmpty(request.SuccessUrl) ? _frontendUrls.PaymentSuccessPath : request.SuccessUrl;
            string cancelUrl = string.IsNullOrEmpty(request.CancelUrl) ? _frontendUrls.OrderPendingPath : request.CancelUrl;

            // =====================================================================
            // 4. RẼ NHÁNH PHƯƠNG THỨC THANH TOÁN LẠI (REPAY)
            // =====================================================================

            if (request.PaymentMethod == PaymentMethod.Wallet)
            {
                // Khách hàng đổi ý, muốn dùng Ví để thanh toán lại đơn đang chờ

                // Trừ tiền và ghi Transaction
                await _walletService.PayOrderGroupWithWalletAsync(userId, orderGroup.Id, orderGroup.TotalGroupAmount);

                // Cập nhật trạng thái
                orderGroup.PaymentStatus = PaymentStatus.Paid;
                foreach (var order in orderGroup.Orders)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Processing; // Đẩy đơn đi tiếp
                }

                //_unitOfWork.OrderGroups.Update(orderGroup);
                await _unitOfWork.CommitAsync();

                return new CheckoutResponse
                {
                    OrderGroupId = orderGroup.Id,
                    TotalAmount = orderGroup.TotalGroupAmount,
                    PaymentUrl = successUrl // Trả thẳng về trang thành công
                };
            }
            else
            {
                // Mặc định hoặc khách vẫn muốn dùng Stripe
                var paymentRequest = new CreateCheckoutSessionRequest
                {
                    OrderGroupId = orderGroup.Id,
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl
                };

                var paymentRes = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);

                return new CheckoutResponse
                {
                    OrderGroupId = orderGroup.Id,
                    TotalAmount = orderGroup.TotalGroupAmount,
                    PaymentUrl = paymentRes.PaymentUrl
                };
            }
        }
        public async Task<OrderResponse> GetOrderDetailAsync(Guid userId, Guid orderId)
        {
            // 1. Gọi Repo lấy dữ liệu
            var order = await _unitOfWork.Orders.GetOrderDetailByIdAsync(orderId);

            // 2. Validate tồn tại
            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            // 3. Validate quyền (Security Check)
            // Người xem phải là người đặt đơn hàng đó
            if (order.CustomerId != userId)
            {
                throw new UnauthorizedAccessException("You are not authorized to view this order.");
            }

            // 4. Map sang DTO
            // Đảm bảo MappingProfile đã map Order -> OrderResponse
            var response = _mapper.Map<OrderResponse>(order);

            return response;
        }

        public async Task CancelAbandonedOrdersAsync(CancellationToken token = default)
        {
            // Cấu hình thời gian quá hạn (Ví dụ: 24 tiếng. Với Stripe có thể set 30 phút tùy bạn)
            var expirationTime = DateTime.UtcNow.AddHours(-_orderSettings.AbandonedOrderTimeoutHours);

            var abandonedOrders = await _unitOfWork.Orders.GetAbandonedOrdersAsync(expirationTime, token);

            if (!abandonedOrders.Any()) return;

            foreach (var order in abandonedOrders)
            {
                // 1. Cập nhật trạng thái
                order.OrderStatus = OrderStatus.Cancelled;
                order.CancelReason = "The order was automatically cancelled because the payment time expired.";

                // 2. Nhả lại kho (Stock) cho Base Product
                foreach (var item in order.OrderItems)
                {
                    await RefundItemStockAsync(item);

                    // 2.1 Nhả lại kho (Stock) cho các linh kiện rời (Nếu là hàng Custom)
                    if (item.IsCustom && item.OrderItemComponents != null)
                    {
                        foreach (var comp in item.OrderItemComponents)
                        {
                            var part = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                            if (part != null)
                            {
                                // Số lượng linh kiện = Số lượng yêu cầu * Số lượng Kit khách mua
                                part.StockQuantity += (comp.Quantity * item.Quantity);
                                await _unitOfWork.Models.UpdateAsync(part);
                            }
                        }
                    }
                }

                // 3. Nhả lại lượt dùng Voucher
                var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(order.Id);
                foreach (var av in appliedVouchers)
                {
                    var voucherToRestore = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                    if (voucherToRestore != null && voucherToRestore.UsedCount > 0)
                    {
                        voucherToRestore.UsedCount--;
                        _unitOfWork.Vouchers.Update(voucherToRestore);
                    }
                }

                await _unitOfWork.Orders.UpdateOrderAsync(order);
            }

            // Lưu toàn bộ thay đổi cùng 1 lúc
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PRIVATE HELPERS
        // =================================================================

        /// <summary>
        /// Factory — tạo OrderItem thường (pure, không side effect).
        /// </summary>
        private static OrderItem BuildNormalOrderItem(Guid orderId, int quantity,
            Guid assembledProductId, string productName, string productImage, decimal productPrice)
        {
            return new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = null, 
                AssembledProductId = assembledProductId, 
                ProductName = productName,
                ProductImage = productImage,
                UnitPrice = productPrice,
                Quantity = quantity,
                TotalPrice = productPrice * quantity,
                IsCustom = false,
                OrderItemComponents = new List<OrderItemComponent>()
            };
        }

        /// <summary>
        /// Factory — tạo OrderItem custom (builder) + Components (pure, không side effect).
        /// </summary>
        private static OrderItem BuildCustomOrderItem(Guid orderId, int quantity,
            Guid productId, string productName, string productImage,
            decimal baseKitPrice, Guid sessionId,
            Dictionary<string, SelectedPartResponse>? selectedParts)
        {
            string? finalPreviewImage = null;
            if (selectedParts != null)
            {
                var stepPriority = new[] { "keycap", "switch", "plate", "case" };
                foreach (var step in stepPriority)
                {
                    if (selectedParts.TryGetValue(step, out var part) && !string.IsNullOrEmpty(part.LayerImageUrl))
                    {
                        finalPreviewImage = part.LayerImageUrl;
                        break;
                    }
                }
            }

            var newItemId = Guid.NewGuid();

            var newItem = new OrderItem
            {
                Id = newItemId,
                OrderId = orderId,
                ProductId = productId,
                ProductName = $"{productName} (Custom Build)",
                ProductImage = finalPreviewImage ?? productImage,
                UnitPrice = baseKitPrice,
                Quantity = quantity,
                IsCustom = true,
                IsDeleted = false,
                DesignConfig = JsonSerializer.Serialize(new
                {
                    SessionId = sessionId,
                    BaseKitId = productId,
                    PreviewImage = finalPreviewImage,
                    CreatedTick = DateTime.UtcNow.Ticks
                }),
                OrderItemComponents = new List<OrderItemComponent>()
            };

            if (selectedParts != null)
            {
                foreach (var part in selectedParts.Values)
                {
                    int qtyRecipe = part.Quantity > 0 ? part.Quantity : 1;
                    newItem.UnitPrice += (part.Price * qtyRecipe);
                    newItem.OrderItemComponents.Add(new OrderItemComponent
                    {
                        Id = Guid.NewGuid(),
                        OrderItemId = newItemId,
                        PartId = part.Id,
                        PartName = part.Name,
                        PartPriceSnapshot = part.Price,
                        PartImageUrl = part.ThumbnailUrl,
                        Quantity = qtyRecipe
                    });
                }
            }

            newItem.TotalPrice = newItem.UnitPrice * newItem.Quantity;
            return newItem;
        }


        private async Task ExecuteRefundStrategyAsync(Order order)
        {
            // A. Hoàn trả tồn kho (Stock)
            // Cần load OrderItems nếu chưa có
            // Lưu ý: Nếu OrderItems chưa được Include trong GetByIdAsync ở trên thì phải load lại hoặc Include ngay từ đầu
            // Giả sử repo đã include OrderItems
            foreach (var item in order.OrderItems)
            {
                await RefundItemStockAsync(item);
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

        public async Task ReleaseFundsForEligibleOrdersAsync(CancellationToken token = default)
        {
            // 1. Xác định thời điểm hết hạn bảo hành (30 ngày trước)
            var warrantyThreshold = DateTime.UtcNow.AddDays(-_orderSettings.WarrantyPeriodDays);

            // 2. Lấy danh sách các đơn đủ điều kiện nhả tiền
            // Điều kiện: Status=Completed, PaymentStatus=Paid (chưa Released), UpdatedAt <= 30 ngày trước
            var eligibleOrders = await _unitOfWork.Orders
                .GetOrdersEligibleForFundReleaseAsync(warrantyThreshold, token);

            if (!eligibleOrders.Any())
                return;

            foreach (var order in eligibleOrders)
            {
                try
                {
                    if (!order.ShopId.HasValue)
                        continue;

                    // 3. Lấy thông tin Shop để get UserId
                    var shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value, token);
                    if (shop == null)
                        continue;

                    // 4. Nhả tiền (chuyển từ HeldBalance -> Balance)
                    // ReleaseHeldMoneyAsync sẽ tự động:
                    // - Update Wallet.HeldBalance và Wallet.Balance
                    // - Tạo Payment log với Type=SalesReleased
                    decimal actualShopRevenue = order.TotalAmount + order.SystemDiscountAmount;
                    await _walletService.ReleaseHeldMoneyAsync(shop.UserId, order.Id, actualShopRevenue);
                    // 5. Đánh dấu đơn đã nhả tiền
                    order.PaymentStatus = PaymentStatus.Released;
                    await _unitOfWork.Orders.UpdateOrderAsync(order, token);
                }
                catch (Exception ex)
                {
                    // Log error để monitoring, nhưng không throw để tiếp tục xử lý order tiếp theo
                    System.Diagnostics.Debug.WriteLine($"Failed to release funds for order {order.Id}: {ex.Message}");
                }
            }

            // 6. Commit tất cả changes
            await _unitOfWork.CommitAsync();
        }

        private async Task<CheckoutResponse> ProcessWalletCheckoutAsync(Guid userId, OrderGroup orderGroup, string successUrl)
        {
            // 1. Gọi sang WalletService để trừ tiền và ghi Transaction
            await _walletService.PayOrderGroupWithWalletAsync(userId, orderGroup.Id, orderGroup.TotalGroupAmount);

            // 2. Cập nhật trạng thái Payment của OrderGroup và các Order lẻ thành Đã Thanh Toán
            orderGroup.PaymentStatus = PaymentStatus.Paid;
            foreach (var order in orderGroup.Orders)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = OrderStatus.Processing;
            }

            //_unitOfWork.OrderGroups.Update(orderGroup);
            await _unitOfWork.CommitAsync();

            // 3. Trả về Response 
            return new CheckoutResponse
            {
                OrderGroupId = orderGroup.Id,
                TotalAmount = orderGroup.TotalGroupAmount,
                PaymentUrl = successUrl
            };
        }

        //===================CHECKOUT============================//
        private async Task<(Order cartOrder, List<OrderItem> selectedItems)> ValidateAndGetCartItemsAsync(Guid userId, List<Guid> selectedOrderItemIds)
        {
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null || !cartOrder.OrderItems.Any())
                throw new InvalidOperationException("Cart is empty.");

            if (selectedOrderItemIds == null || !selectedOrderItemIds.Any())
                throw new InvalidOperationException("Please select at least one item to checkout.");

            var selectedItems = cartOrder.OrderItems
                .Where(i => selectedOrderItemIds.Contains(i.Id))
                .ToList();

            if (!selectedItems.Any() || selectedItems.Count != selectedOrderItemIds.Distinct().Count())
                throw new InvalidOperationException("Some selected items are invalid or not in your cart.");

            return (cartOrder, selectedItems);
        }
        private async Task<List<Voucher>> ValidateAndGetActiveVouchersAsync(Guid cartOrderId)
        {
            var appliedVouchersInCart = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(cartOrderId);
            var activeVouchers = new List<Voucher>();

            foreach (var av in appliedVouchersInCart)
            {
                var voucherCheck = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                if (voucherCheck != null)
                {
                    if (voucherCheck.UsedCount >= voucherCheck.UsageLimit)
                        throw new InvalidOperationException($"The voucher {voucherCheck.Code} has reached its usage limit while in your cart.");

                    if (DateTime.UtcNow > voucherCheck.EndDate)
                        throw new InvalidOperationException($"The voucher {voucherCheck.Code} has expired.");

                    activeVouchers.Add(voucherCheck);
                }
            }

            return activeVouchers;
        }
        private async Task<decimal> CreateOrdersAndDeductStockAsync(Guid userId, CheckoutRequest request, List<OrderItem> selectedItems, OrderGroup orderGroup)
        {
            var shopIdMap = new Dictionary<Guid, Guid>();
            foreach (var item in selectedItems)
            {
                shopIdMap[item.Id] = await GetShopIdForCartItemAsync(item);
            }

            var itemsByShop = selectedItems.GroupBy(i => shopIdMap[i.Id]);

            decimal totalCheckoutSubTotal = 0;

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

                decimal shopSubTotal = 0;

                foreach (var cartItem in shopGroup)
                {
                    if (cartItem.IsCustom)
                    {
                        if (cartItem.ProductId.HasValue)
                        {
                            bool success = await _unitOfWork.Models.UpdateStockAsync(cartItem.ProductId.Value, -cartItem.Quantity);
                            if (!success)
                                throw new InvalidOperationException($"Product '{cartItem.ProductName}' is out of stock or missing.");
                        }
                    }
                    else
                    {
                        if (cartItem.AssembledProductId.HasValue)
                        {
                            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(cartItem.AssembledProductId.Value);
                            if (assembledProduct == null)
                                throw new InvalidOperationException($"Product '{cartItem.ProductName}' is missing.");

                            int currentStock = assembledProduct.Quantity ?? 0;
                            if (currentStock < cartItem.Quantity)
                                throw new InvalidOperationException($"Product '{cartItem.ProductName}' is out of stock.");

                            assembledProduct.Quantity = currentStock - cartItem.Quantity;
                            await _unitOfWork.AssembledProducts.UpdateAsync(assembledProduct);
                        }
                    }

                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = cartItem.ProductId,
                        AssembledProductId = cartItem.AssembledProductId, // LƯU ĐÚNG CỘT Ở ĐÂY
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

                            int requiredQtyPerKit = comp.Quantity;
                            int totalPartNeeded = requiredQtyPerKit * cartItem.Quantity;

                            bool compSuccess = await _unitOfWork.Models.UpdateStockAsync(comp.PartId, -totalPartNeeded);

                            if (!compSuccess && cartItem.ProductId.HasValue)
                            {
                                throw new InvalidOperationException($"Insufficient stock for component: {comp.PartName}");
                            }

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
                    shopSubTotal += orderItem.TotalPrice;
                }

                order.SubTotal = shopSubTotal;
                order.ShippingFee = _orderSettings.DefaultShippingFee;

                totalCheckoutSubTotal += shopSubTotal;
                orderGroup.Orders.Add(order);
            }

            return totalCheckoutSubTotal;
        }
        private async Task<HashSet<Guid>> ApplyVouchersAndCalculateTotalsAsync(Guid userId, OrderGroup orderGroup, List<Voucher> activeVouchers, decimal totalCheckoutSubTotal)
        {
            var systemVouchers = activeVouchers.Where(v => v.Type == VoucherType.Compensation || v.Scope == VoucherScope.System).ToList();
            var shopVouchers = activeVouchers.Where(v => v.Scope == VoucherScope.Shop && (v.Type == VoucherType.Promotion || v.Type == VoucherType.Negotiation)).ToList();

            foreach (var sysVoucher in systemVouchers)
            {
                if (totalCheckoutSubTotal < sysVoucher.MinOrderValue)
                    throw new Exception(
                        $"The order total ({totalCheckoutSubTotal:N0} VND) does not satisfy the minimum requirement ({sysVoucher.MinOrderValue:N0} VND) for applying system voucher {sysVoucher.Code}. Please add more items or remove the voucher."
                    );
            }

            var appliedVoucherIdsToIncrement = new HashSet<Guid>();

            foreach (var order in orderGroup.Orders)
            {
                decimal orderDiscountAmount = 0;
                decimal systemDiscountForThisOrder = 0;
                decimal currentOrderRemain = order.SubTotal;

                var shopInfo = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                Guid shopOwnerId = shopInfo != null ? shopInfo.UserId : Guid.Empty;

                // Xử lý mã của Shop
                var matchedShopVoucher = shopVouchers.FirstOrDefault(v => v.CreatorId == shopOwnerId);
                if (matchedShopVoucher != null)
                {
                    if (order.SubTotal < matchedShopVoucher.MinOrderValue)
                        throw new Exception($"The total value of items from this shop does not meet the minimum requirement to apply this voucher. {matchedShopVoucher.Code}.");

                    decimal shopDiscount = _voucherService.CalculateVoucherDiscount(matchedShopVoucher, order.SubTotal);
                    if (shopDiscount > currentOrderRemain) shopDiscount = currentOrderRemain;

                    await _unitOfWork.VoucherUsageLogs.AddAsync(new VoucherUsageLog
                    {
                        UserId = userId,
                        OrderId = order.Id,
                        VoucherId = matchedShopVoucher.Id,
                        Code = matchedShopVoucher.Code,
                        VoucherType = matchedShopVoucher.Type,
                        DiscountApplied = shopDiscount,
                        ApplyOrder = 1
                    });

                    orderDiscountAmount += shopDiscount;
                    currentOrderRemain -= shopDiscount;
                    appliedVoucherIdsToIncrement.Add(matchedShopVoucher.Id);
                }

                // Xử lý mã của Sàn (Proration)
                decimal weight = totalCheckoutSubTotal > 0 ? (order.SubTotal / totalCheckoutSubTotal) : 0;

                foreach (var sysVoucher in systemVouchers)
                {
                    decimal totalSysDiscount = _voucherService.CalculateVoucherDiscount(sysVoucher, totalCheckoutSubTotal);
                    decimal proratedDiscount = totalSysDiscount * weight;

                    if (proratedDiscount > currentOrderRemain) proratedDiscount = currentOrderRemain;

                    await _unitOfWork.VoucherUsageLogs.AddAsync(new VoucherUsageLog
                    {
                        UserId = userId,
                        OrderId = order.Id,
                        VoucherId = sysVoucher.Id,
                        Code = sysVoucher.Code,
                        VoucherType = sysVoucher.Type,
                        DiscountApplied = proratedDiscount,
                        ApplyOrder = 2
                    });

                    orderDiscountAmount += proratedDiscount;
                    systemDiscountForThisOrder += proratedDiscount;
                    currentOrderRemain -= proratedDiscount;
                    appliedVoucherIdsToIncrement.Add(sysVoucher.Id);
                }

                // Chốt tiền cho Order
                order.DiscountAmount = orderDiscountAmount;
                order.SystemDiscountAmount = systemDiscountForThisOrder;
                order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - order.DiscountAmount);

                orderGroup.TotalGroupAmount += order.TotalAmount;
            }

            return appliedVoucherIdsToIncrement;
        }
        private async Task FinalizeVouchersAndCleanupCartAsync(Order cartOrder, List<OrderItem> selectedItems, HashSet<Guid> appliedVoucherIdsToIncrement)
        {
            foreach (var voucherId in appliedVoucherIdsToIncrement)
            {
                await _unitOfWork.Vouchers.TryIncrementVoucherUsageAsync(voucherId);
            }

            await _unitOfWork.VoucherUsageLogs.DeleteAllByOrderIdAsync(cartOrder.Id);

            foreach (var item in selectedItems)
            {
                _unitOfWork.Orders.DeleteOrderItem(item);
                cartOrder.OrderItems.Remove(item);
            }

            if (!cartOrder.OrderItems.Any())
            {
                _unitOfWork.Orders.Delete(cartOrder);
            }
            else
            {
                cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
                cartOrder.SubTotal = cartOrder.TotalAmount;
                cartOrder.DiscountAmount = 0;
                await _unitOfWork.Orders.UpdateOrderAsync(cartOrder);
            }
        }
        private async Task<CheckoutResponse> ProcessPaymentBranchAsync(Guid userId, PaymentMethod paymentMethod, OrderGroup orderGroup, string successUrl, string cancelUrl, CancellationToken token)
        {
            if (paymentMethod == PaymentMethod.Wallet)
            {
                return await ProcessWalletCheckoutAsync(userId, orderGroup, successUrl);
            }
            else
            {
                var paymentRequest = new CreateCheckoutSessionRequest
                {
                    OrderGroupId = orderGroup.Id,
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl
                };
                var paymentRes = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);

                return new CheckoutResponse
                {
                    OrderGroupId = orderGroup.Id,
                    TotalAmount = orderGroup.TotalGroupAmount,
                    PaymentUrl = paymentRes.PaymentUrl
                };
            }
        }
        private async Task<(Guid Id, string Name, string Image, decimal Price, int Stock, bool IsActive, bool ShopUnavailable)> FetchAssembledProductSnapshotAsync(Guid productId)
        {
            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(productId);
            if (assembledProduct == null) throw new KeyNotFoundException("Assembled product not found.");

            Guid id = assembledProduct.Id;
            string name = assembledProduct.Name;
            string image = assembledProduct.Image1 ?? "";
            decimal price = assembledProduct.Price;
            int stock = assembledProduct.Quantity ?? 0;
            // AssembledProduct không có cờ IsActive, tạm thời mặc định là true
            bool isActive = true;
            // TODO: Hiện tại bảng AssembledProduct không chứa ShopId, tạm thời set false.
            bool shopUnavailable = false;

            return (id, name, image, price, stock, isActive, shopUnavailable);
        }
        private async Task ProcessCustomItemUpdateAsync(OrderItem item, int newQuantity)
        {
            // 1. Commission (ProductId = null)
            if (!item.ProductId.HasValue)
            {
                // Khóa cứng không cho đổi số lượng đơn Commission
                throw new InvalidOperationException("The quantity of a commission order cannot be changed. It has been fixed based on the shop's quotation.");
            }
            // 2. Hàng từ builder session (Có ProductId và Components)
            else
            {
                var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                if (baseKit == null) throw new InvalidOperationException("Base kit not found.");
                if (baseKit.StockQuantity < newQuantity)
                    throw new InvalidOperationException($"Insufficient base kit stock. Available: {baseKit.StockQuantity}");

                decimal currentCustomUnitPrice = baseKit.Price;

                if (item.OrderItemComponents != null && item.OrderItemComponents.Any())
                {
                    foreach (var comp in item.OrderItemComponents)
                    {
                        var part = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                        if (part != null)
                        {
                            int totalPartNeeded = comp.Quantity * newQuantity;
                            if (part.StockQuantity < totalPartNeeded)
                            {
                                throw new InvalidOperationException($"Insufficient stock for component '{part.Name}'. Needed: {totalPartNeeded}, Available: {part.StockQuantity}");
                            }

                            comp.PartPriceSnapshot = part.Price;
                            currentCustomUnitPrice += (part.Price * comp.Quantity);
                        }
                    }
                }

                item.UnitPrice = currentCustomUnitPrice;
                item.Quantity = newQuantity;
                item.TotalPrice = item.Quantity * item.UnitPrice;
            }
        }

        private async Task ProcessAssembledItemUpdateAsync(OrderItem item, int newQuantity)
        {
            if (item.AssembledProductId.HasValue)
            {
                var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
                if (assembledProduct != null)
                {
                    int stockAvailable = assembledProduct.Quantity ?? 0;
                    if (stockAvailable < newQuantity)
                        throw new InvalidOperationException($"Insufficient stock. Available: {stockAvailable}");

                    item.UnitPrice = assembledProduct.Price;
                }
                else
                {
                    throw new InvalidOperationException("Assembled product not found.");
                }
            }
            item.Quantity = newQuantity;
            item.TotalPrice = item.Quantity * item.UnitPrice;
        }
        private async Task<(bool hasStockIssue, bool isPriceChanged)> ValidateCartItemRealtimeAsync(OrderItemResponse itemDto, Order cartOrder)
        {
            // 1. CASE 1: SẢN PHẨM CUSTOM (BUILDER)
            if (itemDto.IsCustom && itemDto.OrderItemComponents != null && itemDto.OrderItemComponents.Any())
            {
                return await ValidateCustomItemRealtimeAsync(itemDto, cartOrder);
            }
            // 2. CASE 2: COMMISSION 
            else if (itemDto.IsCustom && !itemDto.ProductId.HasValue)
            {
                // Commission đã chốt cứng giá và số lượng -> Bỏ qua, không check kho
                return (false, false);
            }
            // 3. CASE 3: ASSEMBLED PRODUCT 
            else if (!itemDto.IsCustom)
            {
                return await ValidateAssembledItemRealtimeAsync(itemDto, cartOrder);
            }

            return (false, false);
        }

        private async Task<(bool hasStockIssue, bool isPriceChanged)> ValidateCustomItemRealtimeAsync(OrderItemResponse itemDto, Order cartOrder)
        {
            bool hasStockIssue = false;
            bool isPriceChanged = false;
            decimal currentCustomTotal = 0;

            if (itemDto.ProductId.HasValue)
            {
                var baseKit = await _unitOfWork.Models.GetByIdAsync(itemDto.ProductId.Value);
                if (baseKit != null)
                {
                    currentCustomTotal += baseKit.Price;
                    if (baseKit.StockQuantity < itemDto.Quantity)
                    {
                        itemDto.Note = $"Base Kit '{baseKit.Name}' is currently out of stock.";
                        hasStockIssue = true;
                    }
                }
            }

            foreach (var compDto in itemDto.OrderItemComponents)
            {
                var part = await _unitOfWork.Models.GetByIdAsync(compDto.PartId);
                if (part != null)
                {
                    int totalPartNeeded = compDto.Quantity * itemDto.Quantity;
                    if (part.StockQuantity < totalPartNeeded)
                    {
                        compDto.Note = $"Only {part.StockQuantity} units are available (Required: {totalPartNeeded}).";
                        itemDto.Note = "Some components are not available in sufficient quantity.";
                        hasStockIssue = true;
                    }

                    compDto.PartPriceSnapshot = part.Price;
                    currentCustomTotal += (part.Price * compDto.Quantity);
                }
            }

            itemDto.UnitPrice = currentCustomTotal;
            itemDto.TotalPrice = itemDto.UnitPrice * itemDto.Quantity;

            var entityItem = cartOrder.OrderItems.FirstOrDefault(x => x.Id == itemDto.OrderItemId);
            if (entityItem != null && entityItem.TotalPrice != itemDto.TotalPrice)
            {
                entityItem.UnitPrice = itemDto.UnitPrice;
                entityItem.TotalPrice = itemDto.TotalPrice;
                isPriceChanged = true;
            }

            return (hasStockIssue, isPriceChanged);
        }
        private async Task<(bool hasStockIssue, bool isPriceChanged)> ValidateAssembledItemRealtimeAsync(OrderItemResponse itemDto, Order cartOrder)
        {
            bool hasStockIssue = false;
            bool isPriceChanged = false;

            var entityItem = cartOrder.OrderItems.FirstOrDefault(x => x.Id == itemDto.OrderItemId);
            if (entityItem != null && entityItem.AssembledProductId.HasValue)
            {
                var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(entityItem.AssembledProductId.Value);
                if (assembledProduct != null)
                {
                    int stockAvailable = assembledProduct.Quantity ?? 0;

                    // NẾU TỒN KHO ÍT HƠN SỐ LƯỢNG TRONG GIỎ HÀNG
                    if (stockAvailable < itemDto.Quantity)
                    {
                        itemDto.Note = $"Product '{assembledProduct.Name}' only has {stockAvailable} units left. Your cart has been updated.";

                        // TỰ ĐỘNG GIẢM SỐ LƯỢNG TRONG GIỎ XUỐNG BẰNG TỒN KHO THỰC TẾ
                        itemDto.Quantity = stockAvailable;
                        hasStockIssue = true;
                    }

                    itemDto.UnitPrice = assembledProduct.Price;
                    itemDto.TotalPrice = itemDto.UnitPrice * itemDto.Quantity; // Tính lại tổng tiền với số lượng mới

                    // LƯU LẠI SỰ THAY ĐỔI XUỐNG DATABASE
                    if (entityItem.TotalPrice != itemDto.TotalPrice || entityItem.Quantity != itemDto.Quantity)
                    {
                        entityItem.UnitPrice = itemDto.UnitPrice;
                        entityItem.Quantity = itemDto.Quantity;
                        entityItem.TotalPrice = itemDto.TotalPrice;
                        isPriceChanged = true;
                    }
                }
                else
                {
                    itemDto.Note = "The product does not exist or has been removed.";
                    hasStockIssue = true;
                }
            }

            return (hasStockIssue, isPriceChanged);
        }

        private async Task<Guid> GetShopIdForCartItemAsync(OrderItem item)
        {
            if (item.IsCustom)
            {
                // 1. Hàng Custom Builder (Có ProductId là BaseKitId)
                if (item.ProductId.HasValue)
                {
                    var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    return baseKit?.ShopId ?? Guid.Empty;
                }
                // 2. Đơn Commission (Không có ProductId, ShopId lưu trong DesignConfig)
                else if (!string.IsNullOrEmpty(item.DesignConfig))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(item.DesignConfig);
                        if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var parsedShopId))
                            return parsedShopId;
                    }
                    catch { }
                }
            }
            else
            {
                // 3. Hàng Assembled Product 
                if (item.AssembledProductId.HasValue)
                {
                    var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
                    var detail = assembledProduct?.ProductAssembledDetails?.FirstOrDefault();
                    if (detail != null)
                    {
                        var baseKit = await _unitOfWork.Models.GetByIdAsync(detail.BaseKitId);
                        return baseKit?.ShopId ?? Guid.Empty;
                    }
                }
            }

            return Guid.Empty;
        }
        private async Task RefundItemStockAsync(OrderItem item)
        {
            if (item.IsCustom)
            {
                if (!item.ProductId.HasValue) return;
                // 1. Hoàn kho cho Base Kit
                var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    await _unitOfWork.Models.UpdateAsync(product);
                }
            }
            else
            {
                if (!item.AssembledProductId.HasValue) return;
                // 2. Hoàn kho cho Assembled Product
                var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
                if (assembledProduct != null)
                {
                    assembledProduct.Quantity = (assembledProduct.Quantity ?? 0) + item.Quantity;
                    await _unitOfWork.AssembledProducts.UpdateAsync(assembledProduct);
                }
            }
        }
    }
}