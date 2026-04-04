using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
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
        private readonly ReputationSettings _reputationSettings;
        private readonly IReputationService _reputationService;
        private readonly IVnPayService _vnPayService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper, IPaymentService paymentService, IVoucherService voucher, IWalletService wallet, IOptions<OrderSettings> orderOptions,
        IOptions<FrontendUrls> urlOptions, IOptions<SystemSettings> systemSettings, IOptions<ReputationSettings> reputationOptions, IReputationService reputationService, IVnPayService vnPayService, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _paymentService = paymentService;
            _voucherService = voucher;
            _walletService = wallet;
            _orderSettings = orderOptions.Value;
            _frontendUrls = urlOptions.Value;
            _systemSettings = systemSettings.Value;
            _reputationSettings = reputationOptions.Value;
            _reputationService = reputationService;
            _vnPayService = vnPayService;
            _httpContextAccessor = httpContextAccessor;
        }

        // =================================================================
        // 1. SHOPPING CART (ADD, GET, REMOVE, UPDATE)
        // =================================================================

        public async Task AddToCartAsync(Guid userId, AddToCartRequest request, CancellationToken token = default)
        {
            if (request.Quantity <= 0) request.Quantity = 1;

            // 1. Lấy hoặc tạo Giỏ hàng
            var cart = await _unitOfWork.Carts.GetCartByUserIdAsync(userId);
            if (cart == null)
            {
                cart = new Cart { CustomerId = userId, CreatedAt = DateTime.UtcNow };
                await _unitOfWork.Carts.AddAsync(cart);
                await _unitOfWork.CommitAsync();
            }

            // 2. Gọi Helper xử lý logic thêm hàng tùy loại
            if (request.BuilderSessionId.HasValue)
            {
                await ProcessAddCustomItemToCartAsync(cart, request);
            }
            else if (!request.IsCustom)
            {
                await ProcessAddNormalItemToCartAsync(cart, request);
            }
            else
            {
                throw new InvalidOperationException("Invalid cart request parameters.");
            }

            await _unitOfWork.CommitAsync();
        }

        public async Task<OrderResponse> GetMyCartAsync(Guid userId, CancellationToken token = default)
        {
            var cart = await _unitOfWork.Carts.GetCartByUserIdAsync(userId);
            if (cart == null || !cart.CartItems.Any()) return null;

            // Sync CartItem với session đang còn active trước khi render giỏ hàng.
            // Đảm bảo user luôn thấy giá và addon mới nhất, không phải snapshot cũ.
            await SyncCustomCartItemsWithSessionsAsync(cart);

            var orderItemsResponse = new List<OrderItemResponse>();
            decimal subTotal = 0;

            foreach (var item in cart.CartItems)
            {
                var itemResponse = await ValidateAndMapCartItemAsync(item);
                orderItemsResponse.Add(itemResponse);
                subTotal += itemResponse.TotalPrice;
            }

            return new OrderResponse
            {
                OrderId = cart.Id,
                OrderStatus = OrderStatus.Pending.ToString(),
                PaymentStatus = PaymentStatus.Pending.ToString(),
                SubTotal = subTotal,
                TotalAmount = subTotal,
                CreatedAt = cart.CreatedAt,
                OrderItems = orderItemsResponse
            };
        }

        public async Task RemoveItemFromCartAsync(Guid userId, Guid orderItemId, CancellationToken token = default)
        {
            var item = await _unitOfWork.CartItems.GetByIdAsync(orderItemId);
            if (item == null) throw new KeyNotFoundException("Item not found in cart.");

            _unitOfWork.CartItems.Remove(item);
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateCartItemQuantityAsync(Guid userId, Guid orderItemId, int newQuantity, CancellationToken token = default)
        {
            if (newQuantity <= 0)
            {
                await RemoveItemFromCartAsync(userId, orderItemId, token);
                return;
            }

            var item = await _unitOfWork.CartItems.GetByIdAsync(orderItemId);
            if (item == null) throw new KeyNotFoundException("Item not found in cart.");

            // Gọi Helper kiểm tra tồn kho
            await ValidateCartItemStockAsync(item, newQuantity);

            item.Quantity = newQuantity;
            _unitOfWork.CartItems.Update(item);

            await _unitOfWork.CommitAsync();
        }

        public async Task<CalculateCartResponse> CalculateCartPreviewAsync(Guid userId, CalculateCartRequest request)
        {
            var response = new CalculateCartResponse();

            var cart = await _unitOfWork.Carts.GetCartByUserIdAsync(userId);
            if (cart == null || !cart.CartItems.Any()) return response;

            var selectedItems = cart.CartItems
                .Where(i => request.SelectedOrderItemIds.Contains(i.Id))
                .ToList();
            if (!selectedItems.Any()) return response;

            // 1. Tính giá Real-time cho các món được tick
            var mappedItems = new List<OrderItemResponse>();
            foreach (var item in selectedItems)
            {
                mappedItems.Add(await ValidateAndMapCartItemAsync(item));
            }

            // 2. Chia theo Shop
            var shopGroups = mappedItems.GroupBy(x => x.ShopId).ToList();
            decimal totalCartSubTotal = 0;
            decimal totalShippingFee = 0;
            decimal totalShopDiscount = 0;

            var shopVoucherGroups = NormalizeShopVoucherCodes(request.AppliedShopVoucherCodes, request.AppliedShopVoucherCodeGroups);

            foreach (var group in shopGroups)
            {
                var shopId = group.Key;
                var items = group.ToList();
                var shop = shopId != Guid.Empty ? await _unitOfWork.Shops.GetByIdAsync(shopId) : null;

                decimal shopSubTotal = items.Sum(x => x.TotalPrice);
                decimal shopShippingFee = _orderSettings.DefaultShippingFee;
                decimal shopDiscount = 0;
                string? shopVoucherError = null;

                if (shopVoucherGroups.TryGetValue(shopId, out var shopVoucherCodes) && shopVoucherCodes.Count > 0)
                {
                    var distinctCodes = shopVoucherCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
                    var vouchers = new List<Voucher>();

                    foreach (var code in distinctCodes)
                    {
                        var sv = await _unitOfWork.Vouchers.GetByCodeAsync(code);
                        if (sv == null)
                        {
                            shopVoucherError = "Voucher not found.";
                            break;
                        }

                        if (sv.Scope != VoucherScope.Shop)
                        {
                            shopVoucherError = "This voucher is not a shop voucher.";
                            break;
                        }

                        if (sv.ShopId.HasValue && sv.ShopId.Value != shopId)
                        {
                            shopVoucherError = "This voucher does not belong to this shop.";
                            break;
                        }

                        shopVoucherError = await ValidateVoucherStrictAsync(userId, sv, shopSubTotal);
                        if (shopVoucherError != null) break;

                        vouchers.Add(sv);
                    }

                    if (shopVoucherError == null && vouchers.Count > 1 && vouchers.Any(v => v.TargetUserId != userId))
                    {
                        shopVoucherError = "Only private shop vouchers can be stacked.";
                    }

                    if (shopVoucherError == null)
                    {
                        decimal remaining = shopSubTotal;
                        foreach (var sv in vouchers)
                        {
                            decimal stepDiscount = _voucherService.CalculateVoucherDiscount(sv, shopSubTotal);
                            if (stepDiscount > remaining) stepDiscount = remaining;
                            shopDiscount += stepDiscount;
                            remaining -= stepDiscount;
                        }
                    }
                }

                response.ShopPreviews.Add(new ShopCartPreviewDto
                {
                    ShopId = shopId,
                    ShopName = shop?.ShopName ?? "Shop",
                    SubTotal = shopSubTotal,
                    ShippingFee = shopShippingFee,
                    ShopDiscountAmount = shopDiscount,
                    TotalAmount = Math.Max(0, shopSubTotal + shopShippingFee - shopDiscount),
                    IncludedOrderItemIds = items.Select(x => x.OrderItemId).ToList(),
                    ShopVoucherError = shopVoucherError
                });

                totalCartSubTotal += shopSubTotal;
                totalShippingFee += shopShippingFee;
                totalShopDiscount += shopDiscount;
            }

            // Tính Voucher của Hệ Thống (CHỈ TÍNH TOÁN, KHÔNG GHI DATABASE)
            decimal systemDiscount = 0;
            string? systemVoucherError = null;
            if (!string.IsNullOrEmpty(request.AppliedSystemVoucherCode))
            {
                var sysV = await _unitOfWork.Vouchers.GetByCodeAsync(request.AppliedSystemVoucherCode);
                if (sysV == null)
                {
                    systemVoucherError = "Voucher not found.";
                }
                else
                {
                    if (sysV.Scope != VoucherScope.System)
                    {
                        systemVoucherError = "This voucher is not a system voucher.";
                    }
                    else
                    {
                        systemVoucherError = await ValidateVoucherStrictAsync(userId, sysV, totalCartSubTotal);
                    }

                    // NẾU KHÔNG CÓ LỖI (null) THÌ MỚI BẮT ĐẦU TÍNH TIỀN GIẢM GIÁ
                    if (systemVoucherError == null)
                    {
                        systemDiscount = sysV.DiscountType == DiscountType.FixedAmount ? sysV.Value : (totalCartSubTotal * sysV.Value / 100);
                        if (sysV.MaxDiscountAmount.HasValue && systemDiscount > sysV.MaxDiscountAmount.Value) systemDiscount = sysV.MaxDiscountAmount.Value;
                        if (systemDiscount > totalCartSubTotal) systemDiscount = totalCartSubTotal;
                    }
                }
            }

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
            var cart = await _unitOfWork.Carts.GetCartByUserIdAsync(userId);
            if (cart == null || !cart.CartItems.Any()) throw new InvalidOperationException("Your cart is empty.");

            var selectedItems = cart.CartItems.Where(i => request.SelectedOrderItemIds.Contains(i.Id)).ToList();
            if (!selectedItems.Any()) throw new InvalidOperationException("No items have been selected.");

            string successUrl = string.IsNullOrEmpty(request.SuccessUrl) ? _frontendUrls.PaymentSuccessPath : request.SuccessUrl;
            string cancelUrl = string.IsNullOrEmpty(request.CancelUrl) ? _frontendUrls.PaymentCancelPath : request.CancelUrl;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var mappedItems = new List<OrderItemResponse>();
                foreach (var item in selectedItems)
                {
                    await ValidateCartItemStockAsync(item, item.Quantity);
                    mappedItems.Add(await ValidateAndMapCartItemAsync(item));
                }

                await EnforceReputationMonthlyLimitsAsync(userId, mappedItems, token);

                var orderGroup = new OrderGroup
                {
                    Id = Guid.NewGuid(),
                    CustomerId = userId,
                    PaymentStatus = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    TotalGroupAmount = 0,
                    Orders = new List<Order>()
                };

                await ProcessCheckoutShopGroupAsync(orderGroup, mappedItems, selectedItems, request);

                decimal totalCheckoutSubTotal = orderGroup.Orders.Sum(o => o.SubTotal);
                await ApplyVouchersToCheckoutAsync(userId, orderGroup, request, totalCheckoutSubTotal);

                // Đã sửa: Dùng CreateAsync thay vì AddAsync
                await _unitOfWork.OrderGroups.CreateAsync(orderGroup, token);

                _unitOfWork.CartItems.RemoveRange(selectedItems);

                await _unitOfWork.CommitAsync();

                if (request.PaymentMethod == PaymentMethod.Wallet)
                {
                    return await ProcessWalletCheckoutAsync(userId, orderGroup, successUrl);
                }
                else
                {
                    string paymentUrl = await GeneratePaymentUrlAsync(
                orderGroup.Id, request.PaymentMethod, successUrl, cancelUrl, userId, token);

                    return new CheckoutResponse
                    {
                        OrderGroupId = orderGroup.Id,
                        TotalAmount = orderGroup.TotalGroupAmount,
                        PaymentUrl = paymentUrl
                    };
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

        public async Task<OrderResponse> GetOrderDetailAsync(Guid userId, Guid orderId)
        {
            var order = await _unitOfWork.Orders.GetOrderDetailByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found.");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("You are not authorized to view this order.");

            var response = _mapper.Map<OrderResponse>(order);
            await EnrichOrderItemsAsync(response, order); // Gọi hàm Helper
            return response;
        }

        public async Task UpdateShippingAddressAsync(Guid userId, Guid orderId, DTOs.Order.UpdateShippingAddressRequest request, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found.");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("Access denied.");

            if (order.OrderStatus == OrderStatus.Shipped ||
                order.OrderStatus == OrderStatus.Completed ||
                order.OrderStatus == OrderStatus.Returning ||
                order.OrderStatus == OrderStatus.Returned ||
                order.OrderStatus == OrderStatus.Refunded ||
                order.OrderStatus == OrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update shipping address after the order has been shipped.");
            }

            order.ReceiverName = request.ReceiverName;
            order.ReceiverPhone = request.ReceiverPhone;
            order.ShippingAddress = request.ShippingAddress;

            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        // API CHO SHOP
        public async Task<OrderResponse> GetShopOrderDetailAsync(Guid shopId, Guid orderId, CancellationToken token = default)
        {
            // ĐỔI TỪ GetByIdAsync SANG GetOrderDetailByIdAsync để lấy đủ Include (Components, v.v...)
            var order = await _unitOfWork.Orders.GetOrderDetailByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.ShopId != shopId) throw new UnauthorizedAccessException("This order does not belong to your shop.");

            var response = _mapper.Map<OrderResponse>(order);
            await EnrichOrderItemsAsync(response, order); // DÙNG CHUNG HÀM HELPER ĐỂ ĐỒNG BỘ DATA CHO SHOP
            return response;
        }

        public async Task CancelOrderByShopAsync(Guid shopId, Guid orderId, string reason, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.ShopId != shopId) throw new UnauthorizedAccessException("Access denied.");

            if (order.OrderStatus == OrderStatus.Shipped ||
                order.OrderStatus == OrderStatus.Completed ||
                order.OrderStatus == OrderStatus.Returning ||
                order.OrderStatus == OrderStatus.Returned ||
                order.OrderStatus == OrderStatus.Refunded ||
                order.OrderStatus == OrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot cancel order at this stage.");
            }

            foreach (var item in order.OrderItems)
            {
                await RefundItemStockAsync(item);
            }

            if (order.PaymentStatus == PaymentStatus.Paid && order.OrderGroupId.HasValue)
            {
                await _paymentService.RefundPaymentAsync(order.OrderGroupId.Value);
                order.PaymentStatus = PaymentStatus.Refunded;
                var group = await _unitOfWork.OrderGroups.GetByIdAsync(order.OrderGroupId.Value);
                if (group != null) group.PaymentStatus = PaymentStatus.Refunded;
            }

            order.OrderStatus = OrderStatus.Cancelled;
            order.CancelReason = reason;
            await _reputationService.AdjustReputationAsync(
                ReputationTargetType.Shop,
                shopId,
                -_reputationSettings.PointsDeductArtisanFault,
                $"Shop cancelled order #{order.Id}");

            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateOrderStatusAsync(Guid shopId, Guid orderId, DTOs.Order.UpdateOrderStatusRequest request, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new KeyNotFoundException("Order not found");
            if (order.ShopId != shopId) throw new UnauthorizedAccessException("Access denied.");
            if (request.Status == OrderStatus.Cancelled && order.OrderStatus != OrderStatus.Cancelled)
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

            if (request.Status == OrderStatus.Shipped)
            {
                if (!request.ExpectedDeliveryDate.HasValue)
                {
                    throw new InvalidOperationException("Expected delivery date is required when marking an order as shipped.");
                }

                order.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
            }

            order.OrderStatus = request.Status;
            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }


        public async Task<OrderIssueResponse> RequestCancelOrderAsync(Guid userId, DTOs.OrderIssues.CancelOrderRequest request, CancellationToken token = default)
        {
            // 1. Validate Order
            var order = await _unitOfWork.Orders.GetByIdAsync(request.OrderId);
            if (order == null) throw new KeyNotFoundException("Order not found.");
            if (order.CustomerId != userId) throw new UnauthorizedAccessException("Not your order.");

            // Chặn nếu đơn hàng đã vào giai đoạn giao hàng hoặc sau đó
            if (order.OrderStatus == OrderStatus.Shipped ||
                order.OrderStatus == OrderStatus.Completed ||
                order.OrderStatus == OrderStatus.Returning ||
                order.OrderStatus == OrderStatus.Returned ||
                order.OrderStatus == OrderStatus.Refunded ||
                order.OrderStatus == OrderStatus.Cancelled)
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
                    bool isShopFault = request.Decision == OrderIssueStatus.AutoCancelled;

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
                        decimal shopReceivedAmount = ShopRevenueCalculator.CalculateShopRevenue(
                            order,
                            _orderSettings.ShopPayoutRate,
                            _orderSettings.SystemVoucherShopShareRate,
                            _orderSettings.SystemVoucherShopShareCap);

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
                            shopReceivedAmount,
                            isCompleted
                        );

                        // BƯỚC 3.2.3: Phân định lỗi & Xử phạt
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

                    if (isShopFault)
                    {
                        if (order.ShopId.HasValue)
                        {
                            await _reputationService.AdjustReputationAsync(
                                ReputationTargetType.Shop,
                                order.ShopId.Value,
                                -_reputationSettings.PointsDeductArtisanFault,
                                $"Auto-cancel order #{order.Id}");
                        }

                        var shopOwner = await _unitOfWork.Users.GetByIdAsync(realActionUserId);
                        if (shopOwner != null)
                        {
                            shopOwner.YMonthlyAutoCancels += 1;
                            shopOwner.TotalAutoCancels += 1;
                            if (shopOwner.YMonthlyAutoCancels >= _reputationSettings.MaxMonthlyAutoCancels)
                            {
                                shopOwner.Status = AccountStatus.Suspended;
                            }
                            await _unitOfWork.Users.UpdateAsync(shopOwner);
                        }
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
                    if (order.ShopId.HasValue)
                    {
                        var shopProfile = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                        if (shopProfile != null)
                        {
                            // công thức: Bù lại tiền System Voucher cho Shop
                            decimal shopRevenue = ShopRevenueCalculator.CalculateShopRevenue(
                                order,
                                _orderSettings.ShopPayoutRate,
                                _orderSettings.SystemVoucherShopShareRate,
                                _orderSettings.SystemVoucherShopShareCap);
                            await _walletService.AddPendingSalesToWalletAsync(shopProfile.UserId, order.Id, shopRevenue);
                        }
                    }
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
                string paymentUrl = await GeneratePaymentUrlAsync(
            orderGroup.Id, request.PaymentMethod, successUrl, cancelUrl, userId, token);

                return new CheckoutResponse
                {
                    OrderGroupId = orderGroup.Id,
                    TotalAmount = orderGroup.TotalGroupAmount,
                    PaymentUrl = paymentUrl
                };
            }
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

                // 3. Không hoàn lại lượt dùng voucher khi đơn bị hủy

                await _unitOfWork.Orders.UpdateOrderAsync(order);
            }

            // Lưu toàn bộ thay đổi cùng 1 lúc
            await _unitOfWork.CommitAsync();
        }

        public async Task AutoCancelOrdersWithoutAssemblyAsync(CancellationToken token = default)
        {
            var threshold = DateTime.UtcNow.AddHours(-_orderSettings.ShopAssemblyInitTimeoutHours);
            var orders = await _unitOfWork.Orders.GetOrdersPendingAssemblyInitAsync(threshold, token);

            if (!orders.Any()) return;

            foreach (var order in orders)
            {
                if (order.OrderStatus != OrderStatus.Processing || !order.ShopId.HasValue)
                    continue;

                bool hasLogs = await _unitOfWork.AssemblyProgressLogs.HasLogsForOrderAsync(order.Id);
                if (hasLogs) continue;

                await AutoCancelOrderForAssemblyTimeoutAsync(order, token);
            }

            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PRIVATE HELPERS
        // =================================================================

        /// <summary>
        /// Factory — tạo OrderItem thường (pure, không side effect).
        /// </summary>
        //private static OrderItem BuildNormalOrderItem(Guid orderId, int quantity,
        //    Guid assembledProductId, string productName, string productImage, decimal productPrice)
        //{
        //    return new OrderItem
        //    {
        //        Id = Guid.NewGuid(),
        //        OrderId = orderId,
        //        ProductId = null, 
        //        AssembledProductId = assembledProductId, 
        //        ProductName = productName,
        //        ProductImage = productImage,
        //        UnitPrice = productPrice,
        //        Quantity = quantity,
        //        TotalPrice = productPrice * quantity,
        //        IsCustom = false,
        //        OrderItemComponents = new List<OrderItemComponent>()
        //    };
        //}

        /// <summary>
        /// Factory — tạo OrderItem custom (builder) + Components (pure, không side effect).
        /// </summary>
        //private static OrderItem BuildCustomOrderItem(Guid orderId, int quantity,
        //    Guid productId, string productName, string productImage,
        //    decimal baseKitPrice, Guid sessionId,
        //    Dictionary<string, SelectedPartResponse>? selectedParts)
        //{
        //    string? finalPreviewImage = null;
        //    if (selectedParts != null)
        //    {
        //        var stepPriority = new[] { "keycap", "switch", "plate", "case" };
        //        foreach (var step in stepPriority)
        //        {
        //            if (selectedParts.TryGetValue(step, out var part) && !string.IsNullOrEmpty(part.LayerImageUrl))
        //            {
        //                finalPreviewImage = part.LayerImageUrl;
        //                break;
        //            }
        //        }
        //    }

        //    var newItemId = Guid.NewGuid();

        //    var newItem = new OrderItem
        //    {
        //        Id = newItemId,
        //        OrderId = orderId,
        //        ProductId = productId,
        //        ProductName = $"{productName} (Custom Build)",
        //        ProductImage = finalPreviewImage ?? productImage,
        //        UnitPrice = baseKitPrice,
        //        Quantity = quantity,
        //        IsCustom = true,
        //        IsDeleted = false,
        //        DesignConfig = JsonSerializer.Serialize(new
        //        {
        //            SessionId = sessionId,
        //            BaseKitId = productId,
        //            PreviewImage = finalPreviewImage,
        //            CreatedTick = DateTime.UtcNow.Ticks
        //        }),
        //        OrderItemComponents = new List<OrderItemComponent>()
        //    };

        //    if (selectedParts != null)
        //    {
        //        foreach (var part in selectedParts.Values)
        //        {
        //            int qtyRecipe = part.Quantity > 0 ? part.Quantity : 1;
        //            newItem.UnitPrice += (part.Price * qtyRecipe);
        //            newItem.OrderItemComponents.Add(new OrderItemComponent
        //            {
        //                Id = Guid.NewGuid(),
        //                OrderItemId = newItemId,
        //                PartId = part.Id,
        //                PartName = part.Name,
        //                PartPriceSnapshot = part.Price,
        //                PartImageUrl = part.ThumbnailUrl,
        //                Quantity = qtyRecipe
        //            });
        //        }
        //    }

        //    newItem.TotalPrice = newItem.UnitPrice * newItem.Quantity;
        //    return newItem;
        //}


        //private async Task ExecuteRefundStrategyAsync(Order order)
        //{
        //    // A. Hoàn trả tồn kho (Stock)
        //    // Cần load OrderItems nếu chưa có
        //    // Lưu ý: Nếu OrderItems chưa được Include trong GetByIdAsync ở trên thì phải load lại hoặc Include ngay từ đầu
        //    // Giả sử repo đã include OrderItems
        //    foreach (var item in order.OrderItems)
        //    {
        //        await RefundItemStockAsync(item);
        //    }

        //    // B. Xử lý tiền (Voucher/Refund)
        //    // Nếu chưa thanh toán -> ko cần làm gì
        //    if (order.PaymentStatus == PaymentStatus.Pending || order.PaymentStatus == PaymentStatus.Pending)
        //    {
        //        order.PaymentStatus = PaymentStatus.Failed;
        //        return;
        //    }

        //    // Nếu đã thanh toán -> Tạo Voucher (TODO)
        //    /* // TODO: Voucher Logic
        //       var voucher = new Voucher { ... };
        //       await _unitOfWork.Vouchers.AddAsync(voucher);
        //    */

        //    // Update trạng thái tiền
        //    order.PaymentStatus = PaymentStatus.Refunded;
        //}

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

                    var customer = await _unitOfWork.Users.GetByIdAsync(order.CustomerId);

                    // 4. Nhả tiền (chuyển từ HeldBalance -> Balance)
                    // ReleaseHeldMoneyAsync sẽ tự động:
                    // - Update Wallet.HeldBalance và Wallet.Balance
                    // - Tạo Payment log với Type=SalesReleased
                    decimal actualShopRevenue = ShopRevenueCalculator.CalculateShopRevenue(
                        order,
                        _orderSettings.ShopPayoutRate,
                        _orderSettings.SystemVoucherShopShareRate,
                        _orderSettings.SystemVoucherShopShareCap);
                    await _walletService.ReleaseHeldMoneyAsync(shop.UserId, order.Id, actualShopRevenue);
                    // 5. Đánh dấu đơn đã nhả tiền
                    order.PaymentStatus = PaymentStatus.Released;
                    await _unitOfWork.Orders.UpdateOrderAsync(order, token);

                    // 6. Cộng điểm uy tín sau bảo hành
                    int successPoints = _reputationSettings.PointsPerSuccessfulOrder;

                    if (customer != null)
                    {
                        int newCustomerScore = await _reputationService.AdjustReputationAsync(
                            ReputationTargetType.Customer,
                            customer.Id,
                            successPoints,
                            $"Order #{order.Id} completed");
                        customer.CurrentReputationScore = newCustomerScore;
                        customer.ConsecutiveSuccesses += 1;

                        if (customer.ConsecutiveSuccesses >= _reputationSettings.SlowResponseResetSuccessCount)
                        {
                            customer.SlowResponseViolationCount = 0;
                            customer.ConsecutiveSuccesses = 0;
                        }

                        await _unitOfWork.Users.UpdateAsync(customer);
                    }
                    await _reputationService.AdjustReputationAsync(
                        ReputationTargetType.Shop,
                        shop.Id,
                        successPoints,
                        $"Order #{order.Id} completed");
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
                if (order.ShopId.HasValue)
                {
                    var shopProfile = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                    if (shopProfile != null)
                    {
                        // Công thức chuẩn: Bù lại tiền System Voucher cho Shop
                        decimal shopRevenue = ShopRevenueCalculator.CalculateShopRevenue(
                            order,
                            _orderSettings.ShopPayoutRate,
                            _orderSettings.SystemVoucherShopShareRate,
                            _orderSettings.SystemVoucherShopShareCap);
                        await _walletService.AddPendingSalesToWalletAsync(shopProfile.UserId, order.Id, shopRevenue);
                    }
                }
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

                var inProgressCount = await _unitOfWork.Orders.CountInProgressOrdersForCustomerShopAsync(userId, shopGroup.Key);
                if (inProgressCount >= _orderSettings.MaxInProgressOrdersPerShopCustomer)
                {
                    throw new InvalidOperationException($"You already have {_orderSettings.MaxInProgressOrdersPerShopCustomer} in-progress orders with this shop.");
                }

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
        //private async Task<(Guid Id, string Name, string Image, decimal Price, int Stock, bool IsActive, bool ShopUnavailable)> FetchAssembledProductSnapshotAsync(Guid productId)
        //{
        //    var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(productId);
        //    if (assembledProduct == null) throw new KeyNotFoundException("Assembled product not found.");

        //    Guid id = assembledProduct.Id;
        //    string name = assembledProduct.Name;
        //    string image = assembledProduct.Image1 ?? "";
        //    decimal price = assembledProduct.Price;
        //    int stock = assembledProduct.Quantity ?? 0;
        //    // AssembledProduct không có cờ IsActive, tạm thời mặc định là true
        //    bool isActive = true;
        //    // TODO: Hiện tại bảng AssembledProduct không chứa ShopId, tạm thời set false.
        //    bool shopUnavailable = false;

        //    return (id, name, image, price, stock, isActive, shopUnavailable);
        //}
        //private async Task ProcessCustomItemUpdateAsync(OrderItem item, int newQuantity)
        //{
        //    // 1. Commission (ProductId = null)
        //    if (!item.ProductId.HasValue)
        //    {
        //        // Khóa cứng không cho đổi số lượng đơn Commission
        //        throw new InvalidOperationException("The quantity of a commission order cannot be changed. It has been fixed based on the shop's quotation.");
        //    }
        //    // 2. Hàng từ builder session (Có ProductId và Components)
        //    else
        //    {
        //        var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
        //        if (baseKit == null) throw new InvalidOperationException("Base kit not found.");
        //        if (baseKit.StockQuantity < newQuantity)
        //            throw new InvalidOperationException($"Insufficient base kit stock. Available: {baseKit.StockQuantity}");

        //        decimal currentCustomUnitPrice = baseKit.Price;

        //        if (item.OrderItemComponents != null && item.OrderItemComponents.Any())
        //        {
        //            foreach (var comp in item.OrderItemComponents)
        //            {
        //                var part = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
        //                if (part != null)
        //                {
        //                    int totalPartNeeded = comp.Quantity * newQuantity;
        //                    if (part.StockQuantity < totalPartNeeded)
        //                    {
        //                        throw new InvalidOperationException($"Insufficient stock for component '{part.Name}'. Needed: {totalPartNeeded}, Available: {part.StockQuantity}");
        //                    }

        //                    comp.PartPriceSnapshot = part.Price;
        //                    currentCustomUnitPrice += (part.Price * comp.Quantity);
        //                }
        //            }
        //        }

        //        item.UnitPrice = currentCustomUnitPrice;
        //        item.Quantity = newQuantity;
        //        item.TotalPrice = item.Quantity * item.UnitPrice;
        //    }
        //}

        //private async Task ProcessAssembledItemUpdateAsync(OrderItem item, int newQuantity)
        //{
        //    if (item.AssembledProductId.HasValue)
        //    {
        //        var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
        //        if (assembledProduct != null)
        //        {
        //            int stockAvailable = assembledProduct.Quantity ?? 0;
        //            if (stockAvailable < newQuantity)
        //                throw new InvalidOperationException($"Insufficient stock. Available: {stockAvailable}");

        //            item.UnitPrice = assembledProduct.Price;
        //        }
        //        else
        //        {
        //            throw new InvalidOperationException("Assembled product not found.");
        //        }
        //    }
        //    item.Quantity = newQuantity;
        //    item.TotalPrice = item.Quantity * item.UnitPrice;
        //}
        //private async Task<(bool hasStockIssue, bool isPriceChanged)> ValidateCartItemRealtimeAsync(OrderItemResponse itemDto, Order cartOrder)
        //{
        //    // 1. CASE 1: SẢN PHẨM CUSTOM (BUILDER)
        //    if (itemDto.IsCustom && itemDto.OrderItemComponents != null && itemDto.OrderItemComponents.Any())
        //    {
        //        return await ValidateCustomItemRealtimeAsync(itemDto, cartOrder);
        //    }
        //    // 2. CASE 2: COMMISSION 
        //    else if (itemDto.IsCustom && !itemDto.ProductId.HasValue)
        //    {
        //        // Commission đã chốt cứng giá và số lượng -> Bỏ qua, không check kho
        //        return (false, false);
        //    }
        //    // 3. CASE 3: ASSEMBLED PRODUCT 
        //    else if (!itemDto.IsCustom)
        //    {
        //        return await ValidateAssembledItemRealtimeAsync(itemDto, cartOrder);
        //    }

        //    return (false, false);
        //}

        //private async Task<(bool hasStockIssue, bool isPriceChanged)> ValidateCustomItemRealtimeAsync(OrderItemResponse itemDto, Order cartOrder)
        //{
        //    bool hasStockIssue = false;
        //    bool isPriceChanged = false;
        //    decimal currentCustomTotal = 0;

        //    if (itemDto.ProductId.HasValue)
        //    {
        //        var baseKit = await _unitOfWork.Models.GetByIdAsync(itemDto.ProductId.Value);
        //        if (baseKit != null)
        //        {
        //            currentCustomTotal += baseKit.Price;
        //            if (baseKit.StockQuantity < itemDto.Quantity)
        //            {
        //                itemDto.Note = $"Base Kit '{baseKit.Name}' is currently out of stock.";
        //                hasStockIssue = true;
        //            }
        //        }
        //    }

        //    foreach (var compDto in itemDto.OrderItemComponents)
        //    {
        //        var part = await _unitOfWork.Models.GetByIdAsync(compDto.PartId);
        //        if (part != null)
        //        {
        //            int totalPartNeeded = compDto.Quantity * itemDto.Quantity;
        //            if (part.StockQuantity < totalPartNeeded)
        //            {
        //                compDto.Note = $"Only {part.StockQuantity} units are available (Required: {totalPartNeeded}).";
        //                itemDto.Note = "Some components are not available in sufficient quantity.";
        //                hasStockIssue = true;
        //            }

        //            compDto.PartPriceSnapshot = part.Price;
        //            currentCustomTotal += (part.Price * compDto.Quantity);
        //        }
        //    }

        //    itemDto.UnitPrice = currentCustomTotal;
        //    itemDto.TotalPrice = itemDto.UnitPrice * itemDto.Quantity;

        //    var entityItem = cartOrder.OrderItems.FirstOrDefault(x => x.Id == itemDto.OrderItemId);
        //    if (entityItem != null && entityItem.TotalPrice != itemDto.TotalPrice)
        //    {
        //        entityItem.UnitPrice = itemDto.UnitPrice;
        //        entityItem.TotalPrice = itemDto.TotalPrice;
        //        isPriceChanged = true;
        //    }

        //    return (hasStockIssue, isPriceChanged);
        //}
        //private async Task<(bool hasStockIssue, bool isPriceChanged)> ValidateAssembledItemRealtimeAsync(OrderItemResponse itemDto, Order cartOrder)
        //{
        //    bool hasStockIssue = false;
        //    bool isPriceChanged = false;

        //    var entityItem = cartOrder.OrderItems.FirstOrDefault(x => x.Id == itemDto.OrderItemId);
        //    if (entityItem != null && entityItem.AssembledProductId.HasValue)
        //    {
        //        var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(entityItem.AssembledProductId.Value);
        //        if (assembledProduct != null)
        //        {
        //            int stockAvailable = assembledProduct.Quantity ?? 0;

        //            // NẾU TỒN KHO ÍT HƠN SỐ LƯỢNG TRONG GIỎ HÀNG
        //            if (stockAvailable < itemDto.Quantity)
        //            {
        //                itemDto.Note = $"Product '{assembledProduct.Name}' only has {stockAvailable} units left. Your cart has been updated.";

        //                // TỰ ĐỘNG GIẢM SỐ LƯỢNG TRONG GIỎ XUỐNG BẰNG TỒN KHO THỰC TẾ
        //                itemDto.Quantity = stockAvailable;
        //                hasStockIssue = true;
        //            }

        //            itemDto.UnitPrice = assembledProduct.Price;
        //            itemDto.TotalPrice = itemDto.UnitPrice * itemDto.Quantity; // Tính lại tổng tiền với số lượng mới

        //            // LƯU LẠI SỰ THAY ĐỔI XUỐNG DATABASE
        //            if (entityItem.TotalPrice != itemDto.TotalPrice || entityItem.Quantity != itemDto.Quantity)
        //            {
        //                entityItem.UnitPrice = itemDto.UnitPrice;
        //                entityItem.Quantity = itemDto.Quantity;
        //                entityItem.TotalPrice = itemDto.TotalPrice;
        //                isPriceChanged = true;
        //            }
        //        }
        //        else
        //        {
        //            itemDto.Note = "The product does not exist or has been removed.";
        //            hasStockIssue = true;
        //        }
        //    }

        //    return (hasStockIssue, isPriceChanged);
        //}

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

        private async Task ProcessAddCustomItemToCartAsync(Cart cart, AddToCartRequest request)
        {
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.BuilderSessionId!.Value);
            if (session == null) throw new KeyNotFoundException("Builder session not found.");
            if (session.CurrentStep != "complete") throw new InvalidOperationException("The builder session is not completed.");

            var baseKit = await _unitOfWork.Models.GetByIdAsync(session.BaseKitId);
            if (baseKit == null || !baseKit.IsActive) throw new InvalidOperationException("Product is inactive or not found.");
            if (baseKit.StockQuantity < request.Quantity) throw new InvalidOperationException($"Insufficient stock. Available: {baseKit.StockQuantity}");

            // Luôn build DesignConfig từ session.SelectedItemsJson hiện tại.
            // Đảm bảo mọi addon đã thêm sau lần add to cart đầu tiên đều được sync vào CartItem.
            var latestDesignConfig = JsonSerializer.Serialize(new
            {
                SessionId = session.Id,
                BaseKitId = session.BaseKitId,
                SelectedItemsJson = session.SelectedItemsJson
            });

            var sessionStr = request.BuilderSessionId.Value.ToString();
            var existingItem = cart.CartItems.FirstOrDefault(x =>
                x.IsCustom && x.DesignConfig != null && x.DesignConfig.Contains(sessionStr));

            if (existingItem != null)
            {
                // Cập nhật cả Quantity LẪN DesignConfig thay vì chỉ tăng Quantity.
                // Trước đây: addon mới bị mất vì snapshot cũ không bị thay thế.
                existingItem.Quantity += request.Quantity;
                existingItem.DesignConfig = latestDesignConfig;
                existingItem.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.CartItems.Update(existingItem);
            }
            else
            {
                var newItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = session.BaseKitId,
                    Quantity = request.Quantity,
                    IsCustom = true,
                    DesignConfig = latestDesignConfig
                };
                await _unitOfWork.CartItems.AddAsync(newItem);
            }
        }

        private async Task ProcessAddNormalItemToCartAsync(Cart cart, AddToCartRequest request)
        {
            if (request.ProductId == null) throw new ArgumentNullException(nameof(request.ProductId));

            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(request.ProductId.Value);
            if (assembledProduct == null) throw new KeyNotFoundException("Product not found.");

            int currentStock = assembledProduct.Quantity ?? 0;
            if (currentStock < request.Quantity) throw new InvalidOperationException($"Insufficient stock. Available: {currentStock}");

            var existingItem = cart.CartItems.FirstOrDefault(x => !x.IsCustom && x.AssembledProductId == request.ProductId.Value);

            if (existingItem != null)
            {
                if (existingItem.Quantity + request.Quantity > currentStock)
                    throw new InvalidOperationException("Insufficient stock for the requested quantity.");

                existingItem.Quantity += request.Quantity;
                _unitOfWork.CartItems.Update(existingItem);
            }
            else
            {
                var newItem = new CartItem
                {
                    CartId = cart.Id,
                    AssembledProductId = request.ProductId.Value,
                    Quantity = request.Quantity,
                    IsCustom = false
                };
                await _unitOfWork.CartItems.AddAsync(newItem);
            }
        }

        private async Task<OrderItemResponse> ValidateAndMapCartItemAsync(CartItem item)
        {
            decimal currentPrice = 0;
            string name = string.Empty;
            string image = string.Empty;
            Guid shopId = Guid.Empty;
            string shopName = string.Empty;
            var componentsDto = new List<OrderItemComponentDto>();

            if (item.IsCustom && item.ProductId.HasValue)
            {
                var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                if (baseKit != null)
                {
                    currentPrice = baseKit.Price;
                    name = $"{baseKit.Name} (Custom Build)";
                    image = baseKit.ThumbnailURL ?? "";
                    shopId = baseKit.ShopId;
                    // Hàm riêng parse JSON linh kiện cộng giá
                    currentPrice += ParseCustomBuilderPrice(item.DesignConfig, componentsDto);
                }
            }
            else if (!item.IsCustom && item.AssembledProductId.HasValue)
            {
                var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
                if (assembledProduct != null)
                {
                    currentPrice = assembledProduct.Price;
                    name = assembledProduct.Name;
                    image = assembledProduct.Image1 ?? "";

                    // Workaround lặp qua chi tiết để lấy ShopId như đã bàn
                    var firstDetail = assembledProduct.ProductAssembledDetails?.FirstOrDefault();
                    var targetModelId = firstDetail?.BaseKitId ?? firstDetail?.ComponentId;
                    if (targetModelId.HasValue)
                    {
                        var relatedModel = await _unitOfWork.Models.GetByIdAsync(targetModelId.Value);
                        shopId = relatedModel?.ShopId ?? Guid.Empty;
                    }
                    if (assembledProduct.ProductAssembledDetails != null && assembledProduct.ProductAssembledDetails.Any())
                    {
                        foreach (var detail in assembledProduct.ProductAssembledDetails)
                        {
                            var partId = detail.ComponentId != Guid.Empty ? detail.ComponentId : detail.BaseKitId;

                            componentsDto.Add(new OrderItemComponentDto
                            {
                                PartId = partId,
                                PartName = detail.Component?.Name ?? detail.BaseKit?.Name ?? "Assembly component",
                                PartPriceSnapshot = detail.Component?.Price ?? detail.BaseKit?.Price ?? 0,
                                PartImageUrl = detail.Component?.ThumbnailURL ?? detail.BaseKit?.ThumbnailURL ?? "",
                                Quantity = detail.Quantity > 0 ? detail.Quantity : 1
                            });
                        }
                    }
                }
            }
            else if (item.IsCustom && !item.ProductId.HasValue && !item.AssembledProductId.HasValue)
            {
                if (!string.IsNullOrEmpty(item.DesignConfig))
                {
                    try
                    {
                        using var doc = JsonSerializer.Deserialize<JsonDocument>(item.DesignConfig);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("Price", out var priceProp)) currentPrice = priceProp.GetDecimal();
                        if (root.TryGetProperty("Title", out var titleProp)) name = titleProp.GetString() ?? "Custom Request";
                        if (root.TryGetProperty("Image", out var imgProp)) image = imgProp.GetString() ?? "";
                        if (root.TryGetProperty("ShopId", out var shopIdProp)) shopId = shopIdProp.GetGuid();
                    }
                    catch { /* Bỏ qua lỗi Parse */ }
                }
            }
            if (shopId != Guid.Empty)
            {
                var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
                shopName = shop?.ShopName ?? "Shop";
            }
            else
            {
                shopName = "Shop";
            }

            return new OrderItemResponse
            {
                OrderItemId = item.Id,
                ProductId = item.ProductId,
                AssembledProductId = item.AssembledProductId,
                ProductName = name,
                ProductImage = image,
                Quantity = item.Quantity,
                UnitPrice = currentPrice,
                TotalPrice = currentPrice * item.Quantity,
                IsCustom = item.IsCustom,
                ShopId = shopId,
                ShopName = shopName,
                OrderItemComponents = componentsDto
            };
        }

        private decimal ParseCustomBuilderPrice(string? designConfigJson, List<OrderItemComponentDto> componentsDto)
        {
            decimal additionalPrice = 0;
            if (string.IsNullOrEmpty(designConfigJson)) return additionalPrice;

            try
            {
                var configObj = JsonSerializer.Deserialize<JsonElement>(designConfigJson);
                if (configObj.TryGetProperty("SelectedItemsJson", out var selectedItemsProp))
                {
                    var selectedItemsStr = selectedItemsProp.GetString();
                    if (!string.IsNullOrEmpty(selectedItemsStr))
                    {
                        var selectedParts = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(selectedItemsStr);
                        if (selectedParts != null)
                        {
                            foreach (var kvp in selectedParts)
                            {
                                var key = kvp.Key;
                                var part = kvp.Value;
                                int qtyRecipe = part.Quantity > 0 ? part.Quantity : 1;

                                // Dùng CÙNG logic với AddExtraPartToSessionAsync và RemoveExtraPartFromSessionAsync:
                                // - Addon key (bắt đầu bằng "addon_"): chỉ cộng phần CHÊNH LỆCH
                                //   VD: switch addon thay thế switch cũ → cộng (newPrice - oldPrice) * qty
                                //   → tránh double-count giá switch workflow + giá switch addon
                                // - Builder step bình thường: cộng toàn bộ price * qty
                                decimal effectivePrice = key.StartsWith("addon_")
                                    ? (part.Price - part.BaseUnitPriceToDeduct)
                                    : part.Price;

                                additionalPrice += effectivePrice * qtyRecipe;

                                componentsDto.Add(new OrderItemComponentDto
                                {
                                    PartId = part.Id,
                                    PartName = part.Name,
                                    PartPriceSnapshot = effectivePrice, // snapshot giá thực tế được tính
                                    PartImageUrl = part.ThumbnailUrl,
                                    Quantity = qtyRecipe
                                });
                            }
                        }
                    }
                }
            }
            catch { /* Ignored if JSON is invalid */ }

            return additionalPrice;
        }

        private async Task ValidateCartItemStockAsync(CartItem item, int newQuantity)
        {
            if (item.IsCustom && item.ProductId.HasValue)
            {
                var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                if (baseKit == null || baseKit.StockQuantity < newQuantity)
                    throw new InvalidOperationException("Insufficient stock for custom base kit.");

                // Kiểm tra tồn kho của từng addon trong DesignConfig.
                // Addon chỉ được soft-check khi thêm vào session, race condition có thể xảy ra.
                // Đây là lần hard-check thực sự trước khi cho phép checkout.
                if (!string.IsNullOrEmpty(item.DesignConfig))
                    await ValidateAddonStockFromDesignConfigAsync(item.DesignConfig, newQuantity);
            }
            else if (!item.IsCustom && item.AssembledProductId.HasValue)
            {
                var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
                int currentStock = assembledProduct?.Quantity ?? 0;
                if (currentStock < newQuantity)
                    throw new InvalidOperationException("Insufficient stock.");
            }
        }

        private async Task AutoCancelOrderForAssemblyTimeoutAsync(Order order, CancellationToken token)
        {
            if (order.OrderStatus == OrderStatus.Cancelled || order.OrderStatus == OrderStatus.Refunded)
                return;

            foreach (var item in order.OrderItems)
            {
                await RefundItemStockAsync(item);
            }

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                await _walletService.RefundToWalletAsync(order.CustomerId, order.TotalAmount, $"Refund for auto-cancelled order #{order.Id}");

                if (order.ShopId.HasValue)
                {
                    var shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value, token);
                    if (shop != null)
                    {
                        decimal shopRevenue = ShopRevenueCalculator.CalculateShopRevenue(
                            order,
                            _orderSettings.ShopPayoutRate,
                            _orderSettings.SystemVoucherShopShareRate,
                            _orderSettings.SystemVoucherShopShareCap);
                        await _walletService.DeductFundsForRefundAsync(shop.UserId, order.Id, shopRevenue, false);
                    }
                }

                order.PaymentStatus = PaymentStatus.Refunded;
                if (order.OrderGroupId.HasValue)
                {
                    var group = await _unitOfWork.OrderGroups.GetByIdAsync(order.OrderGroupId.Value);
                    if (group != null) group.PaymentStatus = PaymentStatus.Refunded;
                }
            }

            order.OrderStatus = OrderStatus.Cancelled;
            order.CancelReason = "System auto-cancel: shop did not start assembly within 48 hours.";
            await _unitOfWork.Orders.UpdateOrderAsync(order, token);

            if (order.ShopId.HasValue)
            {
                var shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value, token);
                if (shop != null)
                {
                    await _reputationService.AdjustReputationAsync(
                        ReputationTargetType.Shop,
                        shop.Id,
                        -_reputationSettings.PointsDeductArtisanFault,
                        $"Auto-cancel order #{order.Id}");

                    var shopOwner = await _unitOfWork.Users.GetByIdAsync(shop.UserId);
                    if (shopOwner != null)
                    {
                        shopOwner.YMonthlyAutoCancels += 1;
                        shopOwner.TotalAutoCancels += 1;
                        if (shopOwner.YMonthlyAutoCancels >= _reputationSettings.MaxMonthlyAutoCancels)
                        {
                            shopOwner.Status = AccountStatus.Suspended;
                        }
                        await _unitOfWork.Users.UpdateAsync(shopOwner);
                    }
                }
            }
        }

        // =================================================================
        // PRIVATE HELPERS CHO CHECKOUT & VOUCHER
        // =================================================================

        private async Task EnforceReputationMonthlyLimitsAsync(Guid userId, List<OrderItemResponse> mappedItems, CancellationToken token)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new InvalidOperationException("User not found.");

            var (isLocked, customerLimit) = GetCustomerMonthlyLimit(user.CurrentReputationScore);
            if (isLocked)
                throw new InvalidOperationException("Your account is locked due to low reputation.");

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            if (customerLimit > 0)
            {
                int currentCount = await _unitOfWork.Orders.CountMonthlyOrdersForCustomerAsync(userId, monthStart, monthEnd, token);
                int newOrders = mappedItems.Select(i => i.ShopId).Where(id => id != Guid.Empty).Distinct().Count();
                if (currentCount + newOrders > customerLimit)
                    throw new InvalidOperationException($"You have reached your monthly order limit ({customerLimit}).");
            }

            var shopIds = mappedItems.Select(i => i.ShopId).Where(id => id != Guid.Empty).Distinct().ToList();
            foreach (var shopId in shopIds)
            {
                var shop = await _unitOfWork.Shops.GetByIdAsync(shopId, token);
                if (shop == null) throw new InvalidOperationException("Shop not found.");

                var (isSuspended, shopLimit) = GetShopMonthlyLimit(shop.CurrentQualityScore);
                if (isSuspended)
                    throw new InvalidOperationException("This shop is temporarily suspended due to low reputation.");

                if (shopLimit > 0)
                {
                    int shopCount = await _unitOfWork.Orders.CountMonthlyOrdersForShopAsync(shopId, monthStart, monthEnd, token);
                    if (shopCount + 1 > shopLimit)
                        throw new InvalidOperationException($"This shop has reached its monthly order limit ({shopLimit}).");
                }
            }
        }

        private (bool IsLocked, int MonthlyLimit) GetCustomerMonthlyLimit(int score)
        {
            if (score >= _reputationSettings.CustomerHighMinScore)
                return (false, _reputationSettings.CustomerHighMonthlyOrderLimit);
            if (score >= _reputationSettings.CustomerMidMinScore)
                return (false, _reputationSettings.CustomerMidMonthlyOrderLimit);
            if (score >= _reputationSettings.CustomerLowMinScore)
                return (false, _reputationSettings.CustomerLowMonthlyOrderLimit);

            return (true, 0);
        }

        private (bool IsSuspended, int MonthlyLimit) GetShopMonthlyLimit(int score)
        {
            if (score >= _reputationSettings.ShopHighMinScore)
                return (false, _reputationSettings.ShopHighMonthlyOrderLimit);
            if (score >= _reputationSettings.ShopMidMinScore)
                return (false, _reputationSettings.ShopMidMonthlyOrderLimit);
            if (score >= _reputationSettings.ShopLowMinScore)
                return (false, _reputationSettings.ShopLowMonthlyOrderLimit);

            return (true, 0);
        }

        private async Task ProcessCheckoutShopGroupAsync(OrderGroup orderGroup, List<OrderItemResponse> mappedItems, List<CartItem> selectedItems, CheckoutRequest request)
        {
            var shopGroups = mappedItems.GroupBy(x => x.ShopId).ToList();

            foreach (var group in shopGroups)
            {
                var items = group.ToList();
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderGroupId = orderGroup.Id,
                    CustomerId = orderGroup.CustomerId,
                    ShopId = group.Key == Guid.Empty ? null : group.Key,
                    ReceiverName = request.ReceiverName,
                    ReceiverPhone = request.ReceiverPhone,
                    ShippingAddress = request.ShippingAddress,
                    Note = request.Note,
                    OrderStatus = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    SubTotal = items.Sum(x => x.TotalPrice),
                    ShippingFee = _orderSettings.DefaultShippingFee,
                    OrderItems = new List<OrderItem>()
                };

                foreach (var mappedItem in items)
                {
                    if (mappedItem.IsCustom && mappedItem.ProductId.HasValue)
                    {
                        bool success = await _unitOfWork.Models.UpdateStockAsync(mappedItem.ProductId.Value, -mappedItem.Quantity);
                        if (!success) throw new InvalidOperationException($"Product '{mappedItem.ProductName}' is out of stock.");

                        if (mappedItem.OrderItemComponents != null && mappedItem.OrderItemComponents.Any())
                        {
                            foreach (var component in mappedItem.OrderItemComponents)
                            {
                                int totalPartNeeded = component.Quantity * mappedItem.Quantity;
                                bool componentSuccess = await _unitOfWork.Models.UpdateStockAsync(component.PartId, -totalPartNeeded);
                                if (!componentSuccess)
                                {
                                    throw new InvalidOperationException($"Insufficient stock for component '{component.PartName}'.");
                                }
                            }
                        }
                    }
                    else if (!mappedItem.IsCustom && mappedItem.AssembledProductId.HasValue)
                    {
                        // Đã sửa: Dùng GetByIdWithDetailsAsync
                        var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(mappedItem.AssembledProductId.Value);
                        if (assembledProduct != null)
                        {
                            if (assembledProduct.Quantity < mappedItem.Quantity) throw new InvalidOperationException($"Sản phẩm '{mappedItem.ProductName}' đã hết hàng.");
                            assembledProduct.Quantity -= mappedItem.Quantity;
                            // Đã sửa: Dùng UpdateAsync thay vì Update
                            await _unitOfWork.AssembledProducts.UpdateAsync(assembledProduct);
                        }
                    }

                    var originalCartItem = selectedItems.First(c => c.Id == mappedItem.OrderItemId);
                    var realOrderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = mappedItem.ProductId,
                        AssembledProductId = mappedItem.AssembledProductId,
                        ProductName = mappedItem.ProductName,
                        ProductImage = mappedItem.ProductImage,
                        UnitPrice = mappedItem.UnitPrice,
                        Quantity = mappedItem.Quantity,
                        TotalPrice = mappedItem.TotalPrice,
                        IsCustom = mappedItem.IsCustom,
                        IsDeleted = false,
                        DesignConfig = originalCartItem.DesignConfig,
                        OrderItemComponents = mappedItem.OrderItemComponents?.Select(c => new OrderItemComponent
                        {
                            Id = Guid.NewGuid(),
                            PartId = c.PartId,
                            PartName = c.PartName,
                            PartPriceSnapshot = c.PartPriceSnapshot,
                            PartImageUrl = c.PartImageUrl,
                            Quantity = c.Quantity,
                            Notes = c.Note
                        }).ToList() ?? new List<OrderItemComponent>()
                    };
                    order.OrderItems.Add(realOrderItem);
                }
                orderGroup.Orders.Add(order);
            }
        }

        private static Dictionary<Guid, List<string>> NormalizeShopVoucherCodes(Dictionary<Guid, string>? singleCodes, Dictionary<Guid, List<string>>? groupedCodes)
        {
            var result = new Dictionary<Guid, List<string>>();

            if (groupedCodes != null && groupedCodes.Count > 0)
            {
                foreach (var kv in groupedCodes)
                {
                    var codes = kv.Value?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList()
                        ?? new List<string>();
                    if (codes.Count > 0)
                    {
                        result[kv.Key] = codes;
                    }
                }

                return result;
            }

            if (singleCodes != null)
            {
                foreach (var kv in singleCodes)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                    {
                        result[kv.Key] = new List<string> { kv.Value.Trim() };
                    }
                }
            }

            return result;
        }

        private async Task ApplyVouchersToCheckoutAsync(Guid userId, OrderGroup group, CheckoutRequest request, decimal totalCheckoutSubTotal)
        {
            var shopVoucherGroups = NormalizeShopVoucherCodes(request.AppliedShopVoucherCodes, request.AppliedShopVoucherCodeGroups);

            // 1. Áp dụng Shop Voucher
            foreach (var order in group.Orders)
            {
                decimal shopDiscount = 0;
                decimal currentOrderRemain = order.SubTotal;
                int applyOrder = 1;

                if (order.ShopId.HasValue && shopVoucherGroups.TryGetValue(order.ShopId.Value, out var shopVoucherCodes))
                {
                    var distinctCodes = shopVoucherCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
                    var vouchers = new List<Voucher>();

                    foreach (var code in distinctCodes)
                    {
                        var sv = await _unitOfWork.Vouchers.GetByCodeAsync(code);
                        if (sv == null)
                            throw new InvalidOperationException("Voucher not found.");

                        if (sv.Scope != VoucherScope.Shop)
                            throw new InvalidOperationException("This voucher is not a shop voucher.");

                        if (sv.ShopId.HasValue && sv.ShopId.Value != order.ShopId.Value)
                            throw new InvalidOperationException("This voucher does not belong to this shop.");

                        string? error = await ValidateVoucherStrictAsync(userId, sv, order.SubTotal);
                        if (error != null) throw new InvalidOperationException($"Lỗi áp mã {code}: {error}");

                        vouchers.Add(sv);
                    }

                    if (vouchers.Count > 1 && vouchers.Any(v => v.TargetUserId != userId))
                        throw new InvalidOperationException("Only private shop vouchers can be stacked.");

                    foreach (var sv in vouchers)
                    {
                        decimal stepDiscount = _voucherService.CalculateVoucherDiscount(sv, order.SubTotal);
                        if (stepDiscount > currentOrderRemain) stepDiscount = currentOrderRemain;

                        await _unitOfWork.VoucherUsageLogs.AddAsync(new VoucherUsageLog
                        {
                            UserId = userId,
                            OrderId = order.Id,
                            VoucherId = sv.Id,
                            Code = sv.Code,
                            VoucherType = sv.Type,
                            DiscountApplied = stepDiscount,
                            ApplyOrder = applyOrder
                        });

                        sv.UsedCount += 1;
                        _unitOfWork.Vouchers.Update(sv);

                        applyOrder += 1;
                        shopDiscount += stepDiscount;
                        currentOrderRemain -= stepDiscount;
                    }
                }

                order.DiscountAmount = shopDiscount;
            }

            // 2. Áp dụng System Voucher
            if (!string.IsNullOrEmpty(request.AppliedSystemVoucherCode))
            {
                var sysV = await _unitOfWork.Vouchers.GetByCodeAsync(request.AppliedSystemVoucherCode);
                if (sysV != null)
                {
                    if (sysV.Scope != VoucherScope.System)
                        throw new InvalidOperationException("This voucher is not a system voucher.");

                    // Gọi Helper kiểm tra
                    string? error = await ValidateVoucherStrictAsync(userId, sysV, totalCheckoutSubTotal);
                    if (error != null) throw new InvalidOperationException($"Lỗi áp mã {request.AppliedSystemVoucherCode}: {error}");

                    decimal totalSysDiscount = sysV.DiscountType == DiscountType.FixedAmount ? sysV.Value : (totalCheckoutSubTotal * sysV.Value / 100);
                    if (sysV.MaxDiscountAmount.HasValue && totalSysDiscount > sysV.MaxDiscountAmount.Value) totalSysDiscount = sysV.MaxDiscountAmount.Value;
                    if (totalSysDiscount > totalCheckoutSubTotal) totalSysDiscount = totalCheckoutSubTotal;

                    sysV.UsedCount++;
                    _unitOfWork.Vouchers.Update(sysV);

                    decimal remainingDiscount = totalSysDiscount;
                    var orderList = group.Orders.ToList();
                    for (int i = 0; i < orderList.Count; i++)
                    {
                        var order = orderList[i];
                        decimal currentOrderRemain = Math.Max(0, order.SubTotal - order.DiscountAmount);
                        if (currentOrderRemain <= 0) continue;

                        decimal appliedToThisOrder = (i == orderList.Count - 1)
                            ? Math.Min(remainingDiscount, currentOrderRemain)
                            : Math.Min(Math.Round(totalSysDiscount * (order.SubTotal / totalCheckoutSubTotal), 2), currentOrderRemain);

                        if (appliedToThisOrder > 0)
                        {
                            order.SystemDiscountAmount = appliedToThisOrder;
                            remainingDiscount -= appliedToThisOrder;

                            int applyOrder = order.DiscountAmount > 0 ? 2 : 1;

                            await _unitOfWork.VoucherUsageLogs.AddAsync(new VoucherUsageLog
                            {
                                UserId = userId,
                                OrderId = order.Id,
                                VoucherId = sysV.Id,
                                Code = sysV.Code,
                                VoucherType = sysV.Type,
                                DiscountApplied = appliedToThisOrder,
                                ApplyOrder = applyOrder
                            });
                        }
                    }
                }
            }

            // 3. Tính toán Tổng Tiền Group
            decimal totalGroupAmount = 0;
            foreach (var order in group.Orders)
            {
                order.TotalAmount = Math.Max(0, order.SubTotal + order.ShippingFee - order.DiscountAmount - order.SystemDiscountAmount);
                totalGroupAmount += order.TotalAmount;
            }
            group.TotalGroupAmount = totalGroupAmount;
        }

        private async Task<string?> ValidateVoucherStrictAsync(Guid userId, Voucher voucher, decimal subTotalToCheck)
        {
            if (voucher.Status != VoucherStatus.Active) return "Voucher is not available.";

            if (voucher.Scope == VoucherScope.System)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null) return "User not found.";

                if (user.CurrentReputationScore < _reputationSettings.CustomerHighMinScore)
                {
                    return $"Admin vouchers require reputation >= {_reputationSettings.CustomerHighMinScore}.";
                }
            }

            // 1. Check Ngày tháng
            if (DateTime.UtcNow < voucher.StartDate || DateTime.UtcNow > voucher.EndDate)
                return "The voucher has expired or is not yet valid.";

            // 2. Check Giới hạn tổng của Hệ thống
            if (voucher.UsedCount >= voucher.UsageLimit)
                return "The voucher has reached its usage limit.";

            // 3. Check Mã riêng tư (Negotiation / Compensation)
            if (voucher.TargetUserId.HasValue && voucher.TargetUserId.Value != userId)
                return "This voucher is not applicable to you.";

            // 4. Check Số tiền tối thiểu
            if (subTotalToCheck < voucher.MinOrderValue)
                return $"Minimum order value: {voucher.MinOrderValue:N0} VND.";

            // 5. Check Giới hạn Cá nhân (Max Uses Per User)
            if (voucher.MaxUsesPerUser.HasValue)
            {
                int userUsage = await _unitOfWork.VoucherUsageLogs.CountUsageByUserAndVoucherAsync(userId, voucher.Id, Guid.Empty);
                if (userUsage >= voucher.MaxUsesPerUser.Value)
                    return "You have reached the usage limit for this voucher.";
            }

            return null; // Null nghĩa là hợp lệ (Pass hết)
        }

        private async Task<string> GeneratePaymentUrlAsync(
    Guid orderGroupId,
    PaymentMethod paymentMethod,
    string successUrl,
    string cancelUrl,
    Guid userId,
    CancellationToken token = default) // Đã bỏ HttpContext ở đây
        {
            var paymentRequest = new CreateCheckoutSessionRequest
            {
                OrderGroupId = orderGroupId,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            if (paymentMethod == PaymentMethod.VnPay)
            {
                // Tự động lấy HttpContext từ hệ thống
                var context = _httpContextAccessor.HttpContext;
                return await _vnPayService.CreatePaymentUrlAsync(paymentRequest, userId, context);
            }
            else // Mặc định là Stripe
            {
                var session = await _paymentService.CreateCheckoutSessionAsync(paymentRequest, token);
                return session.PaymentUrl; // Trả về dạng string
            }
        }
        private async Task EnrichOrderItemsAsync(OrderResponse response, Order order)
        {
            foreach (var itemResponse in response.OrderItems)
            {
                var dbItem = order.OrderItems.FirstOrDefault(x => x.Id == itemResponse.OrderItemId);

                // A. HÀNG CUSTOM BUILD
                if (itemResponse.IsCustom && itemResponse.ProductId.HasValue)
                {
                    var baseKit = await _unitOfWork.Models.GetByIdAsync(itemResponse.ProductId.Value);
                    if (baseKit != null)
                    {
                        if (itemResponse.ShopId == Guid.Empty) itemResponse.ShopId = baseKit.ShopId;
                        if (string.IsNullOrEmpty(itemResponse.ProductImage)) itemResponse.ProductImage = baseKit.ThumbnailURL;
                    }

                    if (itemResponse.OrderItemComponents != null && itemResponse.OrderItemComponents.Any())
                    {
                        foreach (var comp in itemResponse.OrderItemComponents)
                        {
                            var partInfo = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                            if (partInfo != null)
                            {
                                comp.PartName = partInfo.Name;
                                comp.PartImageUrl = partInfo.ThumbnailURL;
                                if (comp.PartPriceSnapshot == 0) comp.PartPriceSnapshot = partInfo.Price;
                            }
                        }
                    }
                }
                // B. HÀNG THƯỜNG (Assembled Product)
                else if (!itemResponse.IsCustom && itemResponse.AssembledProductId.HasValue)
                {
                    var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(itemResponse.AssembledProductId.Value);
                    if (assembledProduct != null)
                    {
                        var firstDetail = assembledProduct.ProductAssembledDetails?.FirstOrDefault();
                        var targetModelId = firstDetail?.BaseKitId ?? firstDetail?.ComponentId;
                        if (targetModelId.HasValue)
                        {
                            var relatedModel = await _unitOfWork.Models.GetByIdAsync(targetModelId.Value);
                            if (relatedModel != null && itemResponse.ShopId == Guid.Empty)
                                itemResponse.ShopId = relatedModel.ShopId;
                        }

                        if (assembledProduct.ProductAssembledDetails != null)
                        {
                            itemResponse.OrderItemComponents = new List<OrderItemComponentDto>();
                            foreach (var detail in assembledProduct.ProductAssembledDetails)
                            {
                                var partId = detail.ComponentId != Guid.Empty ? detail.ComponentId : detail.BaseKitId;
                                itemResponse.OrderItemComponents.Add(new OrderItemComponentDto
                                {
                                    PartId = partId,
                                    PartName = detail.Component?.Name ?? detail.BaseKit?.Name ?? "Assembly component",
                                    PartPriceSnapshot = detail.Component?.Price ?? detail.BaseKit?.Price ?? 0,
                                    PartImageUrl = detail.Component?.ThumbnailURL ?? detail.BaseKit?.ThumbnailURL ?? "",
                                    Quantity = detail.Quantity
                                });
                            }
                        }
                    }
                }
                // C. HÀNG COMMISSION (Báo giá Shop)
                else if (itemResponse.IsCustom && !itemResponse.ProductId.HasValue && !itemResponse.AssembledProductId.HasValue)
                {
                    if (dbItem != null && !string.IsNullOrEmpty(dbItem.DesignConfig))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(dbItem.DesignConfig);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("Title", out var titleProp)) itemResponse.ProductName = titleProp.GetString() ?? "Custom Request";
                            if (root.TryGetProperty("Image", out var imgProp)) itemResponse.ProductImage = imgProp.GetString() ?? "";
                            if (root.TryGetProperty("ShopId", out var shopIdProp)) itemResponse.ShopId = shopIdProp.GetGuid();
                        }
                        catch { /* Bỏ qua lỗi Parse */ }
                    }
                }

                // Cập nhật Tên Shop nếu thiếu
                if (itemResponse.ShopId != Guid.Empty && (string.IsNullOrEmpty(itemResponse.ShopName) || itemResponse.ShopName == "N/A"))
                {
                    var shop = await _unitOfWork.Shops.GetByIdAsync(itemResponse.ShopId);
                    itemResponse.ShopName = shop?.ShopName ?? "Shop";
                }
            }
        }

        /// <summary>
        /// Hard-check tồn kho cho tất cả addon component trong DesignConfig.
        /// Gọi trước checkout để bắt race condition (nhiều user cùng add addon cho 1 item kho).
        /// </summary>
        private async Task ValidateAddonStockFromDesignConfigAsync(string designConfig, int kitQuantity)
        {
            try
            {
                var configObj = JsonSerializer.Deserialize<JsonElement>(designConfig);
                if (!configObj.TryGetProperty("SelectedItemsJson", out var selectedItemsProp)) return;

                var selectedItemsStr = selectedItemsProp.GetString();
                if (string.IsNullOrEmpty(selectedItemsStr)) return;

                var selectedParts = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(selectedItemsStr);
                if (selectedParts == null) return;

                // Chỉ check các key addon, bỏ qua các bước builder thông thường (case, plate, switch...)
                foreach (var kvp in selectedParts.Where(x => x.Key.StartsWith("addon_")))
                {
                    var part = kvp.Value;
                    if (part.BaseUnitPriceToDeduct > part.Price)
                        throw new InvalidOperationException(
                            $"Invalid price configuration for add-on '{part.Name}'. " +
                            $"Deduction cannot exceed component price.");
                    int totalNeeded = part.Quantity * kitQuantity;

                    var partEntity = await _unitOfWork.Models.GetByIdAsync(part.Id);
                    if (partEntity == null)
                        throw new InvalidOperationException($"Add-on component '{part.Name}' no longer exists.");

                    if (partEntity.StockQuantity < totalNeeded)
                        throw new InvalidOperationException(
                            $"Insufficient stock for add-on '{part.Name}'. " +
                            $"Available: {partEntity.StockQuantity}, Required: {totalNeeded}. " +
                            $"Please update your configuration before checking out.");
                }
            }
            catch (JsonException)
            {
                // DesignConfig malformed bỏ qua, để checkout tự xử lý
            }
        }
        /// <summary>
        /// Đồng bộ DesignConfig của các CartItem custom với session đang còn active.
        /// Chỉ sync khi session còn sống và data thực sự thay đổi để tránh DB write thừa.
        /// Nếu session đã hết hạn → giữ nguyên snapshot cuối cùng để user vẫn checkout được.
        /// </summary>
        private async Task SyncCustomCartItemsWithSessionsAsync(Cart cart)
        {
            bool hasChanges = false;

            foreach (var item in cart.CartItems.Where(x =>
                x.IsCustom && x.ProductId.HasValue && !string.IsNullOrEmpty(x.DesignConfig)))
            {
                try
                {
                    var configObj = JsonSerializer.Deserialize<JsonElement>(item.DesignConfig!);
                    if (!configObj.TryGetProperty("SessionId", out var sessionIdProp)) continue;
                    if (!sessionIdProp.TryGetGuid(out var sessionId)) continue;

                    var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(sessionId);

                    // Session hết hạn hoặc không tìm thấy, giữ nguyên snapshot cuối, không làm gì
                    if (session == null || session.ExpiresAt < DateTime.UtcNow) continue;

                    // Build config mới nhất từ session hiện tại
                    var latestDesignConfig = JsonSerializer.Serialize(new
                    {
                        SessionId = session.Id,
                        BaseKitId = session.BaseKitId,
                        SelectedItemsJson = session.SelectedItemsJson
                    });

                    // Chỉ update khi thực sự có thay đổi, tránh DB write thừa
                    if (item.DesignConfig == latestDesignConfig) continue;

                    item.DesignConfig = latestDesignConfig;
                    item.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.CartItems.Update(item);
                    hasChanges = true;
                }
                catch (JsonException)
                {
                    // DesignConfig lỗi JSON bỏ qua item này
                }
            }

            if (hasChanges)
                await _unitOfWork.CommitAsync();
        }
    }
}