using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
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

            var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(cartOrder.Id); // cartOrder là biến lưu order giỏ hàng hiện tại của bạn

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
                // 1. TRƯỜNG HỢP: ĐƠN COMMISSION (ProductId = null)
                if (!item.ProductId.HasValue)
                {
                    // Commission không check kho linh kiện, giữ nguyên UnitPrice đã chốt ban đầu
                    item.Quantity = newQuantity;
                    item.TotalPrice = item.Quantity * item.UnitPrice;
                }
                // 2. TRƯỜNG HỢP: HÀNG TỪ BUILDER SESSION (Có ProductId và Components)
                else
                {
                    var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    if (baseKit == null) throw new InvalidOperationException("Base kit not found.");
                    if (baseKit.StockQuantity < newQuantity)
                        throw new InvalidOperationException($"Insufficient base kit stock. Available: {baseKit.StockQuantity}");

                    decimal currentCustomUnitPrice = baseKit.Price; // Khởi tạo bằng giá Base Kit mới nhất

                    // Check kho và tính tổng giá các linh kiện con
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

                                // Cập nhật lại giá linh kiện phòng khi Shop đổi giá
                                comp.PartPriceSnapshot = part.Price;
                                currentCustomUnitPrice += (part.Price * comp.Quantity);
                            }
                        }
                    }

                    // Gán lại giá và tổng tiền
                    item.UnitPrice = currentCustomUnitPrice;
                    item.Quantity = newQuantity;
                    item.TotalPrice = item.Quantity * item.UnitPrice;
                }
            }
            else
            {
                // 3. TRƯỜNG HỢP: HÀNG THƯỜNG (Base Kit mua lẻ, linh kiện mua lẻ...)
                if (item.ProductId.HasValue)
                {
                    var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    if (product != null)
                    {
                        if (product.StockQuantity < newQuantity)
                            throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");

                        // Update Price Realtime cho hàng thường
                        item.UnitPrice = product.Price;
                    }
                }
                item.Quantity = newQuantity;
                item.TotalPrice = item.Quantity * item.UnitPrice;
            }

            // ==========================================
            // CẬP NHẬT TỔNG TIỀN VÀ XỬ LÝ VOUCHER
            // ==========================================
            cartOrder.TotalAmount = cartOrder.OrderItems.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);
            cartOrder.SubTotal = cartOrder.TotalAmount;

            var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(cartOrder.Id);

            if (appliedVouchers != null && appliedVouchers.Any())
            {
                // Gỡ bỏ toàn bộ voucher để tránh sai lệch tính toán
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

            // 2. Lọc ra các sản phẩm người dùng đã chọn mua
            if (request.SelectedOrderItemIds == null || !request.SelectedOrderItemIds.Any())
            {
                // throw Exception bắt buộc phải chọn.
                throw new InvalidOperationException("Please select at least one item to checkout.");
            }

            var selectedItems = cartOrder.OrderItems
                .Where(i => request.SelectedOrderItemIds.Contains(i.Id))
                .ToList();

            if (!selectedItems.Any())
                throw new InvalidOperationException("Selected items not found in cart.");

            // Validate: Đảm bảo không có ID ảo nào được gửi lên
            if (selectedItems.Count != request.SelectedOrderItemIds.Distinct().Count())
                throw new InvalidOperationException("Some selected items are invalid or not in your cart.");

            // Chuẩn bị URL
            string successUrl = request.SuccessUrl;
            string cancelUrl = request.CancelUrl;
            if (string.IsNullOrEmpty(successUrl) || !successUrl.StartsWith("http"))
                successUrl = "http://localhost:3000/payment/success";
            if (string.IsNullOrEmpty(cancelUrl) || !cancelUrl.StartsWith("http"))
                cancelUrl = "http://localhost:3000/payment/cancel";

            // 3. Tạo Group Order
            var orderGroup = new OrderGroup
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TotalGroupAmount = 0,
                Orders = new List<Order>()
            };

            // Group by Shop dựa trên danh sách ĐÃ CHỌN (selectedItems)
            var itemsByShop = selectedItems.GroupBy(i => i.Product?.ShopId ?? Guid.Empty);

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

                    // Trừ kho
                    product.StockQuantity -= cartItem.Quantity;
                    await _unitOfWork.Models.UpdateAsync(product);

                    // Clone OrderItem từ Cart sang Order chính thức
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

                    // Copy Components nếu có (Custom Product)
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
                                catch { /* Ignore JSON errors */ }
                            }

                            int totalPartNeeded = requiredQtyPerKit * cartItem.Quantity;
                            if (partEntity.StockQuantity < totalPartNeeded)
                                throw new InvalidOperationException($"Insufficient component stock: '{partEntity.Name}'.");

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
                // TODO: Sau này nên thay số cứng 30000 bằng logic tính phí ship
                order.ShippingFee = 30000;
                order.TotalAmount = order.SubTotal + order.ShippingFee;

                orderGroup.Orders.Add(order);
                orderGroup.TotalGroupAmount += order.TotalAmount;
            }

            // 4. Lưu Order Group
            await _unitOfWork.OrderGroups.CreateAsync(orderGroup);
            await _unitOfWork.CommitAsync(); // Dữ liệu đã vào DB

            try
            {
                // 5. Tạo Payment Session
                var paymentRequest = new CreateCheckoutSessionRequest
                {
                    OrderGroupId = orderGroup.Id,
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl 
                };

                var paymentRes = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);

                // CLEANUP: Chỉ xóa những món ĐÃ MUA khỏi giỏ hàng
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
                    await _unitOfWork.Orders.UpdateOrderAsync(cartOrder);
                }

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
                // 1. Xóa OrderGroup vừa tạo 
                _unitOfWork.OrderGroups.Delete(orderGroup);

                // 2. Hoàn lại kho
                foreach (var order in orderGroup.Orders)
                {
                    foreach (var item in order.OrderItems)
                    {
                        // Hoàn kho Product chính
                        var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            await _unitOfWork.Models.UpdateAsync(product);
                        }

                        // Hoàn kho Linh kiện (Components)
                        if (item.OrderItemComponents != null)
                        {
                            foreach (var comp in item.OrderItemComponents)
                            {
                                var part = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                                if (part != null)
                                {
                                    part.StockQuantity += (comp.Quantity * item.Quantity); // Nhân với số lượng cha
                                    await _unitOfWork.Models.UpdateAsync(part);
                                }
                            }
                        }
                    }
                }

                await _unitOfWork.CommitAsync(); // Lưu lệnh Rollback

                throw new InvalidOperationException($"Payment initialization failed: {ex.Message}. Please try again.");
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

            // Đảm bảo Shop luôn được load.
            if (order.Shop == null && order.ShopId.HasValue)
            {
                order.Shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
            }

            // 3. XÁC ĐỊNH NGƯỜI THỰC HIỆN 
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

                        // Check xem đơn đã hoàn thành chưa để biết trừ ví nào
                        bool isCompleted = order.OrderStatus == OrderStatus.Completed;

                        // Lưu ý: realActionUserId lúc này là ID chủ Shop (đã fix ở bước trước)
                        await _walletService.DeductFundsForRefundAsync(
                            realActionUserId,
                            order.Id,
                            order.TotalAmount,
                            isCompleted
                        );

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