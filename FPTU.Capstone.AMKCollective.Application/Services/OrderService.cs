using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
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

            // ══════════════════════════════════════════════════════════════
            // PHASE 1: READ-ONLY — Thu thập dữ liệu cần thiết, snapshot vào biến cục bộ.
            //          Không giữ bất kỳ tracked entity nào cho bước WRITE.
            // ══════════════════════════════════════════════════════════════

            Guid productId;
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

                // Snapshot session data
                sessionId = session.Id;
                sessionCurrentStep = session.CurrentStep;
                sessionBaseKitId = session.BaseKitId;
                sessionSelectedItemsJson = session.SelectedItemsJson;

                var baseKit = await _unitOfWork.Models.GetByIdAsync(session.BaseKitId);
                if (baseKit == null) throw new KeyNotFoundException("Product not found.");

                // Snapshot product data
                productId = baseKit.Id;
                productName = baseKit.Name;
                productImage = baseKit.ThumbnailURL ?? "";
                productPrice = baseKit.Price;
                productStock = baseKit.StockQuantity;
                productIsActive = baseKit.IsActive;
                if (baseKit.Shop != null)
                    shopUnavailable = baseKit.Shop.Status != ShopStatus.Active || !baseKit.Shop.IsActive;
            }
            else
            {
                if (request.ProductId == null) throw new ArgumentNullException(nameof(request.ProductId));
                var product = await _unitOfWork.Models.GetByIdAsync(request.ProductId.Value);
                if (product == null) throw new KeyNotFoundException("Product not found.");

                // Snapshot product data
                productId = product.Id;
                productName = product.Name;
                productImage = product.ThumbnailURL ?? "";
                productPrice = product.Price;
                productStock = product.StockQuantity;
                productIsActive = product.IsActive;
                if (product.Shop != null)
                    shopUnavailable = product.Shop.Status != ShopStatus.Active || !product.Shop.IsActive;
            }

            // Validate từ snapshot (không cần entity nào nữa)
            if (!productIsActive) throw new InvalidOperationException("Product is inactive.");
            if (shopUnavailable) throw new InvalidOperationException("Shop unavailable.");
            if (productStock < request.Quantity)
                throw new InvalidOperationException($"Insufficient stock. Available: {productStock}");

            // ══════════════════════════════════════════════════════════════
            // PHASE 2: WRITE — Clear tracker → Load cart AsNoTracking (READ-ONLY).
            //          Mọi thao tác ghi đều qua stub entity hoặc Add() rõ ràng.
            //          KHÔNG BAO GIỜ ghi qua entity load từ DB → 0% phantom Modified.
            // ══════════════════════════════════════════════════════════════
            _unitOfWork.ClearChangeTracker();

            // Cart loaded AsNoTracking — chỉ để đọc, quyết định logic
            var cartOrder = await _unitOfWork.Orders.GetCartOnlyAsync(userId);

            if (cartOrder == null)
            {
                // ─── CASE A: CHƯA CÓ GIỎ HÀNG → Tạo mới toàn bộ (chỉ INSERT) ───
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
                    cartOrder.OrderItems.Add(BuildCustomOrderItem(cartOrder.Id, request.Quantity, productId, productName, productImage, productPrice, sessionId!.Value, selectedParts));
                }
                else
                {
                    cartOrder.OrderItems.Add(BuildNormalOrderItem(cartOrder.Id, request.Quantity, productId, productName, productImage, productPrice));
                }

                cartOrder.TotalAmount = cartOrder.OrderItems.Sum(i => i.TotalPrice);
                cartOrder.SubTotal = cartOrder.TotalAmount;
                await _unitOfWork.Orders.AddAsync(cartOrder); // Entire graph → Added
                await _unitOfWork.CommitAsync();               // Chỉ INSERT, không UPDATE
            }
            else
            {
                // ─── CASE B: ĐÃ CÓ GIỎ HÀNG → Dùng stub để UPDATE, Add() để INSERT ───
                // cartOrder là AsNoTracking → Change Tracker hoàn toàn sạch.
                OrderItem? itemToInsert = null;

                if (request.BuilderSessionId.HasValue)
                {
                    if (sessionCurrentStep != "complete")
                        throw new InvalidOperationException($"Builder session chưa hoàn tất. Bước hiện tại: '{sessionCurrentStep}'.");
                    var selectedParts = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(sessionSelectedItemsJson!);
                    var sessionIdStr = sessionId!.Value.ToString();

                    var existingItem = cartOrder.OrderItems.FirstOrDefault(x =>
                        x.IsCustom && !x.IsDeleted && x.DesignConfig != null && x.DesignConfig.Contains(sessionIdStr));

                    if (existingItem != null)
                    {
                        // UPDATE via stub → chỉ gửi SET Quantity, UnitPrice, TotalPrice WHERE Id=X
                        int newQty = existingItem.Quantity + request.Quantity;
                        decimal newTotal = existingItem.UnitPrice * newQty;
                        existingItem.Quantity = newQty;       // in-memory cho tính total bên dưới
                        existingItem.TotalPrice = newTotal;
                        _unitOfWork.Orders.UpdateItemQuantity(existingItem.Id, newQty, existingItem.UnitPrice, newTotal);
                    }
                    else
                    {
                        itemToInsert = BuildCustomOrderItem(cartOrder.Id, request.Quantity, productId, productName, productImage, productPrice, sessionId!.Value, selectedParts);
                        cartOrder.OrderItems.Add(itemToInsert); // in-memory cho tính total
                    }
                }
                else
                {
                    var existingItem = cartOrder.OrderItems.FirstOrDefault(oi => oi.ProductId == productId && !oi.IsCustom && !oi.IsDeleted);

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
                        itemToInsert = BuildNormalOrderItem(cartOrder.Id, request.Quantity, productId, productName, productImage, productPrice);
                        cartOrder.OrderItems.Add(itemToInsert);
                    }
                }

                // Persist new item (INSERT vào change tracker)
                if (itemToInsert != null)
                    await _unitOfWork.Orders.AddOrderItemAsync(itemToInsert);

                // Update Order total via stub (UPDATE Orders SET TotalAmount=X WHERE Id=Y)
                decimal cartTotal = cartOrder.OrderItems.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);
                _unitOfWork.Orders.UpdateCartTotal(cartOrder.Id, cartTotal);

                // Kiểm tra xem đơn hàng (giỏ hàng) này có đang áp dụng voucher nào không
                var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(cartOrder.Id); // cartOrder là biến lưu order giỏ hàng hiện tại của bạn

                if (appliedVouchers != null && appliedVouchers.Any())
                {
                    // Nếu có, lập tức gỡ bỏ toàn bộ voucher để tránh sai lệch tính toán.
                    // Hàm này bên VoucherService đã có sẵn logic trả lại UsedCount, xóa OrderVoucher và cập nhật TotalAmount.
                    await _voucherService.RemoveAllVouchersAsync(userId, cartOrder.Id);
                }
                await _unitOfWork.CommitAsync(); // Chỉ có stub UPDATE + optional INSERT, 0 phantom
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

            // 3. Duyệt qua từng sản phẩm trong giỏ để Validate Real-time
            foreach (var itemDto in result.OrderItems)
            {
                // ---------------------------------------------------------
                // CASE 1: SẢN PHẨM CUSTOM (BUILDER) - ƯU TIÊN SỐ 1
                // ---------------------------------------------------------
                if (itemDto.IsCustom && itemDto.OrderItemComponents != null && itemDto.OrderItemComponents.Any())
                {
                    decimal currentCustomTotal = 0;

                    // A. Check giá Base Kit (nếu Kit cũng tính tiền và có ID)
                    if (itemDto.ProductId.HasValue)
                    {
                        var baseKit = await _unitOfWork.Models.GetByIdAsync(itemDto.ProductId.Value);
                        if (baseKit != null)
                        {
                            currentCustomTotal += baseKit.Price;
                            // Nếu Base Kit hết hàng
                            if (baseKit.StockQuantity < itemDto.Quantity)
                            {
                                itemDto.Note = $"Base Kit '{baseKit.Name}' is currently out of stock.";
                                hasStockIssue = true;
                            }
                        }
                    }

                    // B. Check từng linh kiện con (Switch, Keycap...)
                    foreach (var compDto in itemDto.OrderItemComponents)
                    {
                        var part = await _unitOfWork.Models.GetByIdAsync(compDto.PartId);

                        if (part != null)
                        {
                            // Tính tổng số lượng linh kiện cần: (Số lượng mỗi phím) * (Số lượng phím đặt mua)
                            int totalPartNeeded = compDto.Quantity * itemDto.Quantity;

                            // Check Kho: Nếu kho < số cần thiết
                            if (part.StockQuantity < totalPartNeeded)
                            {
                                compDto.Note = $"Only {part.StockQuantity} units are available (Required: {totalPartNeeded}).";
                                itemDto.Note = "Some components are not available in sufficient quantity.";
                                // Đánh dấu item cha
                                hasStockIssue = true;
                            }

                            // Check Giá: Cập nhật giá mới nhất nếu Shop có thay đổi giá linh kiện
                            // (Logic: Cart luôn hiển thị giá mới nhất)
                            compDto.PartPriceSnapshot = part.Price;

                            // Cộng dồn vào tổng tiền set Custom
                            currentCustomTotal += (part.Price * compDto.Quantity);
                        }
                    }

                    // Cập nhật lại giá tổng của món Custom này theo thời giá hiện tại
                    itemDto.UnitPrice = currentCustomTotal;
                    itemDto.TotalPrice = itemDto.UnitPrice * itemDto.Quantity;
                }

                // ---------------------------------------------------------
                // CASE 2: ASSEMBLED PRODUCT  -- ĐỂ ĐÂY CHỨ CHƯA BIẾT LÀM SAO 
                // ---------------------------------------------------------
                

            }
               

            // 4. Tính lại tổng tiền giỏ hàng (Sau khi đã update giá các item)
            result.TotalAmount = result.OrderItems.Sum(i => i.TotalPrice);
            result.DiscountAmount = cartOrder.DiscountAmount;
            result.TotalAmount = Math.Max(0, result.SubTotal - result.DiscountAmount);
            // (Optional) Nếu logic Discount phức tạp thì gọi Service tính lại, tạm thời set 0 hoặc giữ nguyên
            // result.DiscountAmount = ...; 

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

            var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(cartOrder.Id); // cartOrder là biến lưu order giỏ hàng hiện tại của bạn

            if (appliedVouchers != null && appliedVouchers.Any())
            {
                // Nếu có, lập tức gỡ bỏ toàn bộ voucher để tránh sai lệch tính toán.
                // Hàm này bên VoucherService đã có sẵn logic trả lại UsedCount, xóa OrderVoucher và cập nhật TotalAmount.
                await _voucherService.RemoveAllVouchersAsync(userId, cartOrder.Id);
            }
            // 5. Lưu thay đổi
            // Lúc này EF sẽ thực hiện lệnh DELETE thật sự
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
            cartOrder.SubTotal = cartOrder.TotalAmount;
            var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(cartOrder.Id); // cartOrder là biến lưu order giỏ hàng hiện tại của bạn

            if (appliedVouchers != null && appliedVouchers.Any())
            {
                // Nếu có, lập tức gỡ bỏ toàn bộ voucher để tránh sai lệch tính toán.
                // Hàm này bên VoucherService đã có sẵn logic trả lại UsedCount, xóa OrderVoucher và cập nhật TotalAmount.
                await _voucherService.RemoveAllVouchersAsync(userId, cartOrder.Id);
            }
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // 2. CHECKOUT & CUSTOMER ORDERS
        // =================================================================

        public async Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken token = default)
        {
            // 1. Lấy giỏ hàng hiện tại
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null || !cartOrder.OrderItems.Any())
                throw new InvalidOperationException("Cart is empty.");

            if (request.SelectedOrderItemIds == null || !request.SelectedOrderItemIds.Any())
                throw new InvalidOperationException("Please select at least one item to checkout.");

            var selectedItems = cartOrder.OrderItems
                .Where(i => request.SelectedOrderItemIds.Contains(i.Id))
                .ToList();

            if (!selectedItems.Any() || selectedItems.Count != request.SelectedOrderItemIds.Distinct().Count())
                throw new InvalidOperationException("Some selected items are invalid or not in your cart.");

            // --- MỤC 4: VALIDATE HẠN SỬ DỤNG VÀ CHUẨN BỊ DANH SÁCH VOUCHER ---
            var appliedVouchersInCart = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(cartOrder.Id);
            var activeVouchers = new List<Voucher>();

            foreach (var av in appliedVouchersInCart)
            {
                var voucherCheck = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                if (voucherCheck != null)
                {
                    if ((voucherCheck.UsedCount - 1) >= voucherCheck.UsageLimit)
                        throw new InvalidOperationException($"The voucher {voucherCheck.Code} has reached its usage limit while in your cart.");

                    if (DateTime.UtcNow > voucherCheck.EndDate)
                        throw new InvalidOperationException($"The voucher {voucherCheck.Code} has expired.");

                    activeVouchers.Add(voucherCheck);
                }
            }

            // Chuẩn bị URL
            string successUrl = string.IsNullOrEmpty(request.SuccessUrl) || !request.SuccessUrl.StartsWith("http") ? "http://localhost:3000/payment/success" : request.SuccessUrl;
            string cancelUrl = string.IsNullOrEmpty(request.CancelUrl) || !request.CancelUrl.StartsWith("http") ? "http://localhost:3000/payment/cancel" : request.CancelUrl;

            // 2. KHỞI TẠO ORDER GROUP
            var orderGroup = new OrderGroup
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TotalGroupAmount = 0,
                Orders = new List<Order>()
            };

            var itemsByShop = selectedItems.GroupBy(i => i.Product?.ShopId ?? Guid.Empty);
            decimal totalCheckoutSubTotal = 0;

            // BƯỚC 2.1: TẠO ĐƠN HÀNG LẺ CHO TỪNG SHOP VÀ TÍNH TỔNG TIỀN GỐC (SUBTOTAL)
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
                    var product = await _unitOfWork.Models.GetByIdAsync(cartItem.ProductId);
                    if (product == null) throw new InvalidOperationException($"Product {cartItem.ProductName} missing.");
                    if (product.StockQuantity < cartItem.Quantity) throw new InvalidOperationException($"Out of stock: {product.Name}");

                    // Trừ kho
                    product.StockQuantity -= cartItem.Quantity;
                    await _unitOfWork.Models.UpdateAsync(product);

                    // Clone OrderItem
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

                    // Copy Components (Custom Product)
                    if (cartItem.OrderItemComponents != null && cartItem.OrderItemComponents.Any())
                    {
                        foreach (var comp in cartItem.OrderItemComponents)
                        {
                            var partEntity = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                            if (partEntity == null) throw new InvalidOperationException($"Component {comp.PartName} not found.");

                            // Logic tính recipe giữ nguyên
                            int requiredQtyPerKit = 1;
                            if (!string.IsNullOrEmpty(product.Specifications))
                            {                     
                                try
                                {
                                    using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(product.Specifications))
                                    {
                                        if (doc.RootElement.TryGetProperty("recipe", out System.Text.Json.JsonElement recipe))
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
                                catch { /* Ignore */ }
                            }

                            int totalPartNeeded = requiredQtyPerKit * cartItem.Quantity;
                            if (partEntity.StockQuantity < totalPartNeeded) throw new InvalidOperationException($"Insufficient stock: {partEntity.Name}");

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
                    shopSubTotal += orderItem.TotalPrice;
                }

                order.SubTotal = shopSubTotal;
                order.ShippingFee = 30000; // Thay bằng logic tính ship sau này

                totalCheckoutSubTotal += shopSubTotal;
                orderGroup.Orders.Add(order);
            }

            // =====================================================================
            // BƯỚC 3: KẾT TOÁN VOUCHER - BÀI TOÁN PHÂN BỔ (PRORATION)
            // =====================================================================

            var systemVouchers = activeVouchers.Where(v => v.Type == VoucherType.Compensation).ToList();
            var shopVouchers = activeVouchers.Where(v => v.Type == VoucherType.Promotion || v.Type == VoucherType.Negotiation).ToList();

            // 3.0 KIỂM TRA LẠI ĐIỀU KIỆN MÃ HỆ THỐNG TRÊN TỔNG CÁC MÓN ĐÃ CHỌN
            foreach (var sysVoucher in systemVouchers)
            {
                if (totalCheckoutSubTotal < sysVoucher.MinOrderValue)
                    throw new InvalidOperationException($"Tổng tiền các món bạn chọn ({totalCheckoutSubTotal:N0}đ) không đủ điều kiện tối thiểu ({sysVoucher.MinOrderValue:N0}đ) để dùng mã hệ thống {sysVoucher.Code}. Vui lòng chọn thêm sản phẩm hoặc bỏ mã giảm giá.");
            }

            foreach (var order in orderGroup.Orders)
            {
                decimal orderDiscountAmount = 0;
                decimal currentOrderRemain = order.SubTotal;

                // FIX BUG: Lấy thông tin Shop để lấy ra UserId của chủ Shop
                var shopInfo = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                Guid shopOwnerId = shopInfo != null ? shopInfo.UserId : Guid.Empty;

                // 3.1. ÁP MÃ CỦA ĐÚNG SHOP ĐÓ (Sử dụng shopOwnerId thay vì order.ShopId)
                var matchedShopVoucher = shopVouchers.FirstOrDefault(v => v.CreatorId == shopOwnerId);
                if (matchedShopVoucher != null)
                {
                    // Kiểm tra lại: Món hàng của riêng Shop này CÓ ĐƯỢC CHỌN ĐỦ MinOrderValue KHÔNG?
                    if (order.SubTotal < matchedShopVoucher.MinOrderValue)
                        throw new InvalidOperationException($"Tổng tiền các món bạn chọn từ Shop {shopInfo?.ShopName} không đủ điều kiện tối thiểu để dùng mã {matchedShopVoucher.Code}.");

                    decimal shopDiscount = _voucherService.CalculateVoucherDiscount(matchedShopVoucher, order.SubTotal);
                    if (shopDiscount > currentOrderRemain) shopDiscount = currentOrderRemain; // Cap tiền giảm

                    await _unitOfWork.OrderVouchers.AddAsync(new OrderVoucher
                    {
                        OrderId = order.Id,
                        VoucherId = matchedShopVoucher.Id,
                        VoucherCode = matchedShopVoucher.Code,
                        VoucherType = matchedShopVoucher.Type,
                        DiscountApplied = shopDiscount,
                        ApplyOrder = 1
                    });

                    orderDiscountAmount += shopDiscount;
                    currentOrderRemain -= shopDiscount;
                }

                // 3.2. ÁP MÃ CỦA SÀN & PHÂN BỔ (PRORATION)
                decimal weight = totalCheckoutSubTotal > 0 ? (order.SubTotal / totalCheckoutSubTotal) : 0;

                foreach (var sysVoucher in systemVouchers)
                {
                    decimal totalSysDiscount = _voucherService.CalculateVoucherDiscount(sysVoucher, totalCheckoutSubTotal);
                    decimal proratedDiscount = totalSysDiscount * weight;

                    if (proratedDiscount > currentOrderRemain) proratedDiscount = currentOrderRemain; // Cap tiền giảm

                    await _unitOfWork.OrderVouchers.AddAsync(new OrderVoucher
                    {
                        OrderId = order.Id,
                        VoucherId = sysVoucher.Id,
                        VoucherCode = sysVoucher.Code,
                        VoucherType = sysVoucher.Type,
                        DiscountApplied = proratedDiscount,
                        ApplyOrder = 2
                    });

                    orderDiscountAmount += proratedDiscount;
                    currentOrderRemain -= proratedDiscount;
                }

                // 3.3. CHỐT TIỀN CHO ĐƠN NÀY (Đã trừ mọi khoản discount)
                order.DiscountAmount = orderDiscountAmount;
                order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - order.DiscountAmount);

                orderGroup.TotalGroupAmount += order.TotalAmount;
            }

            // =====================================================================

            // 4. Lưu dữ liệu OrderGroup
            await _unitOfWork.OrderGroups.CreateAsync(orderGroup);

            // Xóa OrderVouchers nháp của Giỏ hàng (Cart)
            await _unitOfWork.OrderVouchers.DeleteAllByOrderIdAsync(cartOrder.Id);

            // Dọn dẹp giỏ hàng
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
                cartOrder.VoucherId = null;
                await _unitOfWork.Orders.UpdateOrderAsync(cartOrder);
            }

            await _unitOfWork.CommitAsync();

            try
            {
                // 5. Gọi Stripe
                var paymentRequest = new CreateCheckoutSessionRequest { OrderGroupId = orderGroup.Id, SuccessUrl = successUrl, CancelUrl = cancelUrl };
                var paymentRes = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);
                return new CheckoutResponse { OrderGroupId = orderGroup.Id, TotalAmount = orderGroup.TotalGroupAmount, PaymentUrl = paymentRes.PaymentUrl };
            }
            catch (Exception ex)
            {
                // Rollback Kho và Xóa OrderGroup nếu Payment lỗi
                _unitOfWork.OrderGroups.Delete(orderGroup);
                foreach (var order in orderGroup.Orders)
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            await _unitOfWork.Models.UpdateAsync(product);
                        }
                        if (item.OrderItemComponents != null)
                        {
                            foreach (var comp in item.OrderItemComponents)
                            {
                                var part = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                                if (part != null)
                                {
                                    part.StockQuantity += (comp.Quantity * item.Quantity);
                                    await _unitOfWork.Models.UpdateAsync(part);
                                }
                            }
                        }
                    }
                }
                await _unitOfWork.CommitAsync();
                throw new InvalidOperationException($"Payment initialization failed: {ex.Message}");
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
            if (order == null) throw new KeyNotFoundException("Order not found.");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("Not your order.");

            // Chặn nếu đơn hàng đã giao hoặc hoàn tất
            if (order.OrderStatus == OrderStatus.Shipped || order.OrderStatus == OrderStatus.Completed)
            {
                throw new InvalidOperationException("Cannot cancel order at this stage.");
            }

            // 2. Check Spam: Hủy >= 4 đơn/tuần
            var lastWeek = DateTime.UtcNow.AddDays(-7);
            int cancelledCount = await _unitOfWork.OrderIssues.CountUserIssuesAsync(
                userId,
                OrderIssueStatus.AutoCancelled, 
                lastWeek
            );

            bool isSpamRequest = cancelledCount >= 4;

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
                    order.OrderStatus = OrderStatus.Cancelled;
                    order.CancelReason = request.Decision == OrderIssueStatus.AutoCancelled
                                         ? "Request timeout 24h (Auto-Refund)"
                                         : $"Shop approved: {issue.Reason}";

                    // --- 3.1 TRẢ HÀNG VỀ KHO ---
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

                    // --- 3.2 XỬ LÝ VOUCHER & TẠO VOUCHER REFUND (BAO GỒM PHẠT SHOP) ---
                    // Chắc chắn đơn đã Paid nên không cần check if (PaymentStatus == Paid) nữa, 
                    // nhưng muốn an toàn thì vẫn giữ.
                    if (order.PaymentStatus == PaymentStatus.Paid)
                    {
                        var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(order.Id);
                        decimal oldCompensationUsed = 0;

                        foreach (var av in appliedVouchers)
                        {
                            var appliedVoucher = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                            if (appliedVoucher != null)
                            {
                                // 1. Tiền đền bù cũ -> Ghi nhận để gộp vào mã mới
                                if (appliedVoucher.Type == VoucherType.Compensation)
                                {
                                    oldCompensationUsed += av.DiscountApplied;
                                }
                                // 2. Mã Khuyến mãi / Thương lượng -> Hoàn lại 1 lượt cho hệ thống
                                else if (appliedVoucher.Type == VoucherType.Promotion || appliedVoucher.Type == VoucherType.Negotiation)
                                {
                                    if (appliedVoucher.UsedCount > 0 && DateTime.UtcNow <= appliedVoucher.EndDate)
                                    {
                                        appliedVoucher.UsedCount--;
                                        _unitOfWork.Vouchers.Update(appliedVoucher);
                                    }
                                }
                            }
                        }

                        // Bước B: Tính toán Dòng tiền
                        decimal cashPaidAmount = order.TotalAmount;

                        // Mức phạt Shop hủy đơn (Ví dụ 10%)
                        decimal penaltyRate = 0.10m;
                        decimal penaltyAmount = cashPaidAmount * penaltyRate;

                        // Tổng Voucher hoàn lại = Tiền thật + Tiền đền bù cũ + Tiền phạt Shop
                        decimal totalRefundVoucherValue = cashPaidAmount + oldCompensationUsed + penaltyAmount;

                        // Bước C: Tạo Voucher Refund
                        if (totalRefundVoucherValue > 0)
                        {
                            await _voucherService.CreateCompensationVoucherAsync(
                                realActionUserId,
                                issue.UserId,
                                totalRefundVoucherValue
                            );
                        }

                        // Bước D: Trừ tiền ví Shop 
                        bool isCompleted = order.OrderStatus == OrderStatus.Completed;

                        // D1: Rút lại tiền hàng
                        await _walletService.DeductFundsForRefundAsync(
                            realActionUserId,
                            order.Id,
                            cashPaidAmount,
                            isCompleted
                        );

                        // D2: Trừ tiền phạt (Penalty)
                        if (penaltyAmount > 0)
                        {
                            await _walletService.AdjustBalanceAsync(
                                realActionUserId,
                                 new AdjustBalanceRequest
                                 {
                                     UserId = realActionUserId,
                                     Amount = -penaltyAmount,
                                     Reason = $"Cancellation fee for Order #{order.Id}"
                                 }
                            );
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

        public async Task<string> RepayAsync(Guid userId, Guid orderGroupId, CancellationToken token = default)
        {
            // 1. Lấy thông tin đơn hàng 
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(orderGroupId);

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

            // 4. Tạo Stripe Session mới
            // Lưu ý: Cấu hình URL trả về cho Repay có thể khác Checkout gốc (tuỳ bạn)
            var paymentRequest = new CreateCheckoutSessionRequest
            {
                OrderGroupId = orderGroup.Id,
                SuccessUrl = "http://localhost:3000/payment/success",
                CancelUrl = "http://localhost:3000/orders?status=pending" 
            };

            var paymentRes = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);

            return paymentRes.PaymentUrl;
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
            var expirationTime = DateTime.UtcNow.AddHours(-24);

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
                    var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        await _unitOfWork.Models.UpdateAsync(product);
                    }

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
                var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(order.Id);
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
            Guid productId, string productName, string productImage, decimal productPrice)
        {
            return new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = productId,
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

        public async Task ReleaseFundsForEligibleOrdersAsync(CancellationToken token = default)
        {
            // 1. Xác định thời điểm hết hạn bảo hành (30 ngày trước)
            var warrantyThreshold = DateTime.UtcNow.AddDays(-30);

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
                    await _walletService.ReleaseHeldMoneyAsync(shop.UserId, order.Id, order.TotalAmount);

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
    }
}