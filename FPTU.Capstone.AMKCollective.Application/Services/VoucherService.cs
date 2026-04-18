using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class VoucherService :IVoucherService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly VoucherSettings _voucherSettings;
        private readonly ReputationSettings _reputationSettings;

        public VoucherService(IUnitOfWork unitOfWork, IMapper mapper, IOptions<VoucherSettings> voucherOptions, IOptions<ReputationSettings> reputationOptions)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _voucherSettings = voucherOptions.Value;
            _reputationSettings = reputationOptions.Value;
        }

        // 1. Create Promotional Voucher (Marketing)
        public async Task<VoucherResponse> CreatePromotionalVoucherAsync(Guid userId, CreateVoucherRequest request)
        {
            // Validate: Code must be unique
            var exists = await _unitOfWork.Vouchers.CodeExistsAsync(request.Code);
            if (exists)
                throw new InvalidOperationException(
                    $"Voucher code '{request.Code}' already exists. Please choose a different code.");

            var voucher = _mapper.Map<Voucher>(request);
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;    

            if (isAdmin)
            {
                voucher.CreatorId = userId;
                voucher.Scope = VoucherScope.System;
                voucher.ShopId = null;
            }
            else
            {
                voucher.CreatorId = userId;
                voucher.Scope = VoucherScope.Shop;

                var myShop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
                if (myShop == null)
                    throw new UnauthorizedAccessException("Shop not found.");

                if (myShop.CurrentQualityScore < _reputationSettings.ShopMidMinScore)
                    throw new Exception("Shop reputation is too low to create vouchers.");

                voucher.ShopId = myShop?.Id;
            }
            voucher.MaxUsesPerUser = request.MaxUsesPerUser;

            // ÉP CỨNG LOẠI VOUCHER CHỐNG GIAN LẬN 
            // Bất kể Front-end truyền lên Type là gì, API này chỉ được phép tạo mã Promotion
            voucher.Type = VoucherType.Promotion;

            // Default logic for Promotion
            voucher.Status = VoucherStatus.Active;
            voucher.UsedCount = 0;

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            var creator = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("User not found.");
            voucher.Creator = creator;

            return _mapper.Map<VoucherResponse>(voucher).ConvertDatesToLocal();
        }

        // 2. Create Negotiation Voucher - For shop to finalize deals
        public async Task<VoucherResponse> CreateNegotiationVoucherAsync(Guid userId, Guid targetUserId, decimal discountAmount, decimal minOrderValue)
        {
            // Generate random code: NEGO_ + 8 random characters
            string code = "NEGO_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // Kiểm tra quyền và lấy ShopId chuẩn xác
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("User not found or session is invalid.");
            bool isAdmin = currentUser.Role?.Name == RoleType.Admin;

            if (isAdmin)
            {
                throw new UnauthorizedAccessException("Admin cannot create negotiation vouchers.");
            }

            var scope = VoucherScope.Shop;
            Guid? actualShopId = null;

            var myShop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (myShop == null)
                throw new UnauthorizedAccessException("Shop not found.");

            if (myShop.CurrentQualityScore < _reputationSettings.ShopMidMinScore)
                throw new Exception("Shop reputation is too low to create vouchers.");

            actualShopId = myShop?.Id;

            // 2. Khởi tạo Voucher
            var voucher = new Voucher
            {
                Code = code,
                Name = "Negotiation Voucher",
                Description = "Special discount based on negotiation",
                Type = VoucherType.Negotiation,
                DiscountType = DiscountType.FixedAmount,
                Value = discountAmount,
                MaxDiscountAmount = null,
                MinOrderValue = minOrderValue, // Constraint: Must purchase the agreed amount

                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(_voucherSettings.NegotiationValidityDays), // Expires in 7 days to close the deal
                UsageLimit = 1,
                UsedCount = 0,
                Status = VoucherStatus.Active,

                CreatorId = userId, 
                TargetUserId = targetUserId, // Only this customer can use

                Scope = scope,
                ShopId = actualShopId,

                // Negotiation: can only stack with Compensation voucher
                IsStackable = true,
                StackingPolicy = StackingPolicy.WithCompensationOnly
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            voucher.Creator = currentUser;
            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 3. Create Compensation Voucher - For system/shop cancellations
        public async Task<VoucherResponse> CreateCompensationVoucherAsync(Guid userId, Guid targetUserId, decimal refundAmount)
        {
            // Generate code: REFUND_ + ...
            string code = "REFUND_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // 1. Kiểm tra quyền và lấy ShopId chuẩn xác
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("User not found or session is invalid.");
            bool isAdmin = currentUser.Role?.Name == RoleType.Admin;

            var scope = VoucherScope.Shop;
            Guid? actualShopId = null;

            if (isAdmin)
            {
                scope = VoucherScope.System;
            }
            else
            {
                var myShop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
                actualShopId = myShop?.Id;
            }

            // 2. Khởi tạo Voucher
            var voucher = new Voucher
            {
                Code = code,
                Name = "Refund Voucher",
                Description = "Compensation for cancelled order",
                Type = VoucherType.Compensation,
                DiscountType = DiscountType.FixedAmount,
                Value = refundAmount,
                MaxDiscountAmount = null,
                MinOrderValue = 0, // No minimum order value, can be used on any order

                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(_voucherSettings.CompensationValidityMonths),
                UsageLimit = 1,
                UsedCount = 0,
                Status = VoucherStatus.Active,

                CreatorId = userId,
                TargetUserId = targetUserId,
                Scope = scope,
                ShopId = actualShopId,

                // Compensation: can stack with all other voucher types
                IsStackable = true,
                StackingPolicy = StackingPolicy.All
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            voucher.Creator = currentUser;
            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 4. Apply Voucher to Order (Stacking)
        public async Task<ApplyVoucherResult> ApplyVoucherAsync(Guid userId, Guid orderId, string code)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized to modify this order.");

            // [P3-3] Chỉ cho phép apply voucher khi giỏ hàng còn ở trạng thái InCart
            if (order.OrderStatus != OrderStatus.InCart)
                throw new InvalidOperationException("Vouchers can only be modified on an active shopping cart.");

            var incomingVoucher = await _unitOfWork.Vouchers.GetByCodeAsync(code);
            if (incomingVoucher == null) throw new Exception("Voucher not found.");

            // --- BASIC VALIDATION ---
            if (incomingVoucher.MaxUsesPerUser.HasValue)
            {
                int userUsageCount = await _unitOfWork.VoucherUsageLogs.CountUsageByUserAndVoucherAsync(userId, incomingVoucher.Id, orderId);

                if (userUsageCount >= incomingVoucher.MaxUsesPerUser.Value)
                    throw new Exception("You have reached the usage limit for this voucher.");
            }
            if (incomingVoucher.Status != VoucherStatus.Active) throw new Exception("Voucher is not active.");
            if (DateTime.UtcNow < incomingVoucher.StartDate || DateTime.UtcNow > incomingVoucher.EndDate) throw new Exception("Voucher is expired or not yet started.");
            if (incomingVoucher.UsedCount >= incomingVoucher.UsageLimit) throw new Exception("Voucher usage limit reached.");
            if (incomingVoucher.TargetUserId != null && incomingVoucher.TargetUserId != userId)
                throw new Exception("This voucher is not applicable to you.");


            // --- STACKING VALIDATION ---
            var appliedList = (await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(orderId)).ToList();

            if (appliedList.Any())
                throw new Exception("Only one voucher can be applied per order.");

            if (appliedList.Any(av => av.VoucherId == incomingVoucher.Id))
                throw new Exception("This voucher has already been applied to this order.");

            var existingVouchers = new List<Voucher>();
            foreach (var av in appliedList)
            {
                var v = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                if (v != null) existingVouchers.Add(v);
            }

            // 1. Kiểm tra cờ IsStackable chung
            if (!incomingVoucher.IsStackable && existingVouchers.Any())
                throw new Exception("This voucher cannot be stacked with others.");
            if (existingVouchers.Any(v => !v.IsStackable))
                throw new Exception("An existing applied voucher does not allow stacking.");

            // 2. Phân loại mã đang muốn áp dụng
            bool isIncomingCompensation = incomingVoucher.Type == VoucherType.Compensation;
            bool isIncomingSystemPromo = incomingVoucher.Scope == VoucherScope.System;
            bool isIncomingShopVoucher = incomingVoucher.Scope == VoucherScope.Shop;
            

            // 3. KIỂM TRA GIỚI HẠN, HỖ TRỢ MULTI-SHOP
            if (!isIncomingCompensation)
            {
                if (isIncomingSystemPromo)
                {
                    // Chỉ được 1 mã của Sàn cho toàn bộ giỏ hàng
                    if (existingVouchers.Any(v => v.Scope == VoucherScope.System && v.Type == VoucherType.Promotion))
                        throw new Exception("You can only apply ONE System Promotion voucher per order.");
                }
                else if (isIncomingShopVoucher)
                {
                    // MULTI-SHOP: Chỉ chặn nếu TRONG CÙNG 1 SHOP (CreatorId) đã có mã
                    if (existingVouchers.Any(v => v.CreatorId == incomingVoucher.CreatorId &&
                                            (v.Type == VoucherType.Promotion || v.Type == VoucherType.Negotiation)))
                    {
                        throw new Exception("You can only apply ONE Shop voucher per specific shop in this cart.");
                    }

                    // (Tùy chọn) Kiểm tra xem Shop đó có hàng trong giỏ không
                    bool hasItemFromThisShop = false;
                    foreach (var i in order.OrderItems)
                    {
                        Guid sId = Guid.Empty;
                        if (i.ProductId.HasValue)
                        {
                            var productTask = await _unitOfWork.Models.GetByIdAsync(i.ProductId.Value);
                            if (productTask != null) sId = productTask.ShopId;
                        }
                        else if (!string.IsNullOrEmpty(i.DesignConfig))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(i.DesignConfig);
                                if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var parsedShopId))
                                    sId = parsedShopId;
                            }
                            catch { }
                        }

                        if (sId != Guid.Empty)
                        {
                            // DÙNG AWAIT thay vì .Result
                            var shopTask = await _unitOfWork.Shops.GetByIdAsync(sId);
                            if (shopTask != null && shopTask.UserId == incomingVoucher.CreatorId)
                            {
                                hasItemFromThisShop = true;
                                break; // Đã tìm thấy ít nhất 1 món của shop này thì dừng vòng lặp ngay
                            }
                        }
                    }

                    if (!hasItemFromThisShop)
                        throw new Exception("You do not have any items from this shop in your cart to apply this voucher.");
                }
            }

            // --- PREPARE CALCULATION PIPELINE ---
            var allVouchersToApply = new List<Voucher>();
            foreach (var av in appliedList)
            {
                var v = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                if (v != null) allVouchersToApply.Add(v);
            }
            allVouchersToApply.Add(incomingVoucher);

            // Force ordering: Promotion/Negotiation first (0), Compensation last (1)
            allVouchersToApply = allVouchersToApply.OrderBy(v => v.Type == VoucherType.Compensation ? 1 : 0).ToList();

            await _unitOfWork.VoucherUsageLogs.DeleteAllByOrderIdAsync(orderId);

            // --- BẮT ĐẦU CÔ LẬP SỐ TIỀN (ISOLATION CALCULATION) ---
            decimal totalDiscountAmount = 0;
            var newAppliedList = new List<VoucherUsageLog>();

            // 1. Lấy danh sách các món hàng trong giỏ
            var cartItems = order.OrderItems.Where(i => !i.IsDeleted).ToList();
            order.SubTotal = cartItems.Sum(i => i.TotalPrice);
            // 2. Tính tổng tiền của từng Shop (Dựa trên UserId của chủ shop)
            var creatorSubTotals = new Dictionary<Guid, decimal>(); // Key: UserId của chủ Shop, Value: Tổng tiền

            var shopCache = new Dictionary<Guid, Guid>(); // Cache phụ: Map ShopId -> UserId để giảm thiểu gọi DB
            foreach (var item in cartItems)
            {
                Guid shopId = Guid.Empty;

                if (item.ProductId.HasValue)
                {
                    var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    if (product != null) shopId = product.ShopId;
                }
                else if (!string.IsNullOrEmpty(item.DesignConfig))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(item.DesignConfig);
                        if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var parsedShopId))
                            shopId = parsedShopId;
                    }
                    catch { }
                }

                if (shopId != Guid.Empty)
                {
                    Guid shopOwnerUserId = Guid.Empty;

                    if (!shopCache.ContainsKey(shopId))
                    {
                        var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
                        if (shop != null)
                        {
                            shopOwnerUserId = shop.UserId;
                            shopCache[shopId] = shop.UserId;
                        }
                    }
                    else
                    {
                        shopOwnerUserId = shopCache[shopId];
                    }

                    if (shopOwnerUserId != Guid.Empty)
                    {
                        if (!creatorSubTotals.ContainsKey(shopOwnerUserId))
                            creatorSubTotals[shopOwnerUserId] = 0;

                        creatorSubTotals[shopOwnerUserId] += item.TotalPrice;
                    }
                }
            }

            // 3. Xử lý tính toán cho từng Voucher
            for (int i = 0; i < allVouchersToApply.Count; i++)
            {
                var currentVoucher = allVouchersToApply[i];

                // Mặc định: Lấy tổng giỏ hàng (Áp dụng cho mã Sàn/Đền bù)
                decimal baseCalculationAmount = order.SubTotal;

                // KIỂM TRA NẾU ĐÂY LÀ MÃ CỦA SHOP
                if (currentVoucher.Scope == VoucherScope.Shop &&
                    (currentVoucher.Type == VoucherType.Promotion || currentVoucher.Type == VoucherType.Negotiation))
                {
                    // Lấy ngay tổng tiền đã tính sẵn từ Dictionary ra
                    baseCalculationAmount = creatorSubTotals.ContainsKey(currentVoucher.CreatorId)
                                            ? creatorSubTotals[currentVoucher.CreatorId]
                                            : 0;

                    // Chặn: Nếu tổng tiền hàng của riêng Shop này chưa đủ điều kiện
                    if (baseCalculationAmount < currentVoucher.MinOrderValue)
                    {
                        throw new Exception($"Your order total does not meet the minimum requirement of {currentVoucher.MinOrderValue:N0} VND to apply voucher {currentVoucher.Code}.");
                    }
                }
                else
                {
                    // Mã Hệ Thống (Sàn/Đền bù): Kiểm tra trên tổng giỏ hàng
                    if (baseCalculationAmount < currentVoucher.MinOrderValue)
                    {
                        throw new Exception($"Your order does not meet the minimum requirement of {currentVoucher.MinOrderValue:N0} VND to use voucher {currentVoucher.Code}.");
                    }
                }

                // Tính số tiền giảm dựa trên cái baseCalculationAmount đã được cô lập
                decimal stepDiscount = CalculateVoucherDiscount(currentVoucher, baseCalculationAmount);

                // Cap 1: Mã của Shop không được giảm lố số tiền hàng của chính Shop đó
                if (currentVoucher.Type == VoucherType.Promotion || currentVoucher.Type == VoucherType.Negotiation)
                {
                    if (stepDiscount > baseCalculationAmount) stepDiscount = baseCalculationAmount;
                }

                // Cap 2: Tổng mọi discount không được vượt quá số tiền còn lại của đơn hàng chung
                if (stepDiscount > (order.SubTotal - totalDiscountAmount))
                {
                    stepDiscount = order.SubTotal - totalDiscountAmount;
                }

                // Ghi log vào OrderVoucher
                var orderVoucher = new VoucherUsageLog
                {
                    UserId = userId, 
                    OrderId = orderId,
                    VoucherId = currentVoucher.Id,
                    Code = currentVoucher.Code,
                    VoucherType = currentVoucher.Type,
                    DiscountApplied = stepDiscount,
                    ApplyOrder = i + 1
                };

                await _unitOfWork.VoucherUsageLogs.AddAsync(orderVoucher);
                newAppliedList.Add(orderVoucher);

                totalDiscountAmount += stepDiscount;
                // [P2-1] UsedCount KHÔNG tăng ở đây. Sẽ được tăng atomic tại Checkout.
            }

            // --- UPDATE ORDER ---
            order.DiscountAmount = totalDiscountAmount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscountAmount);
            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            return BuildApplyResult(order, newAppliedList);
        }

        // 5. Remove a specific voucher from order
        public async Task<ApplyVoucherResult> RemoveSpecificVoucherAsync(Guid userId, Guid orderId, string voucherCode)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized.");

            if (order.OrderStatus != OrderStatus.InCart)
                throw new InvalidOperationException("Vouchers can only be modified on an active shopping cart.");

            var voucherToRemove = await _unitOfWork.Vouchers.GetByCodeAsync(voucherCode);
            if (voucherToRemove == null) throw new Exception("Voucher not found.");

            var orderVoucherToRemove = await _unitOfWork.VoucherUsageLogs.GetByOrderAndVoucherAsync(orderId, voucherToRemove.Id);
            if (orderVoucherToRemove == null) throw new Exception("This voucher is not applied to the order.");

            // Remove the bridge record
            _unitOfWork.VoucherUsageLogs.Delete(orderVoucherToRemove);

            // [P2-2] Không giảm UsedCount khi remove khỏi cart.
            // UsedCount chỉ được tăng tại thời điểm Checkout, nên không cần hoàn lại ở đây.

            // Recalculate remaining vouchers to ensure caps are still accurate
            var remainingOrderVouchers = (await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(orderId))
                .Where(ov => ov.Id != orderVoucherToRemove.Id)
                .OrderBy(ov => ov.ApplyOrder)
                .ToList();

            decimal totalDiscountAmount = 0;
            var cartItems = order.OrderItems.Where(i => !i.IsDeleted).ToList();
            order.SubTotal = cartItems.Sum(i => i.TotalPrice);
            // Tính tổng tiền của từng Shop 1 lần duy nhất
            var creatorSubTotals = new Dictionary<Guid, decimal>();
            var shopCache = new Dictionary<Guid, Guid>();

            foreach (var item in cartItems)
            {
                Guid shopId = Guid.Empty;

                if (item.ProductId.HasValue)
                {
                    var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    if (product != null) shopId = product.ShopId;
                }
                else if (!string.IsNullOrEmpty(item.DesignConfig))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(item.DesignConfig);
                        if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var parsedShopId))
                            shopId = parsedShopId;
                    }
                    catch { }
                }

                if (shopId != Guid.Empty)
                {
                    Guid shopOwnerUserId = Guid.Empty;
                    if (!shopCache.ContainsKey(shopId))
                    {
                        var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
                        if (shop != null)
                        {
                            shopOwnerUserId = shop.UserId;
                            shopCache[shopId] = shop.UserId;
                        }
                    }
                    else
                    {
                        shopOwnerUserId = shopCache[shopId];
                    }

                    if (shopOwnerUserId != Guid.Empty)
                    {
                        if (!creatorSubTotals.ContainsKey(shopOwnerUserId))
                            creatorSubTotals[shopOwnerUserId] = 0;
                        creatorSubTotals[shopOwnerUserId] += item.TotalPrice;
                    }
                }
            }

            // Tính toán lại các mã voucher còn lại
            for (int i = 0; i < remainingOrderVouchers.Count; i++)
            {
                var ov = remainingOrderVouchers[i];
                var underlyingVoucher = await _unitOfWork.Vouchers.GetByIdAsync(ov.VoucherId);

                if (underlyingVoucher != null)
                {
                    decimal baseCalculationAmount = order.SubTotal;

                    if (underlyingVoucher.Scope == VoucherScope.Shop &&
                       (underlyingVoucher.Type == VoucherType.Promotion || underlyingVoucher.Type == VoucherType.Negotiation))
                    {
                        // Lấy tổng tiền đã tính sẵn cho chủ Shop này
                        baseCalculationAmount = creatorSubTotals.ContainsKey(underlyingVoucher.CreatorId)
                                                ? creatorSubTotals[underlyingVoucher.CreatorId]
                                                : 0;
                    }

                    decimal stepDiscount = CalculateVoucherDiscount(underlyingVoucher, baseCalculationAmount);

                    if (underlyingVoucher.Scope == VoucherScope.Shop &&
                       (underlyingVoucher.Type == VoucherType.Promotion || underlyingVoucher.Type == VoucherType.Negotiation))
                    {
                        if (stepDiscount > baseCalculationAmount) stepDiscount = baseCalculationAmount;
                    }

                    if (stepDiscount > (order.SubTotal - totalDiscountAmount))
                    {
                        stepDiscount = order.SubTotal - totalDiscountAmount;
                    }

                    ov.DiscountApplied = stepDiscount;
                    ov.ApplyOrder = i + 1;
                    _unitOfWork.VoucherUsageLogs.Update(ov);

                    totalDiscountAmount += stepDiscount;
                }
            }

            // Update Order totals
            order.DiscountAmount = totalDiscountAmount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscountAmount);

            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            return BuildApplyResult(order, remainingOrderVouchers);
        }

        // 6. Remove all vouchers from order
        public async Task RemoveAllVouchersAsync(Guid userId, Guid orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized.");

            var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(orderId);

            // [P2-2] Không giảm UsedCount khi remove all khỏi cart.
            // UsedCount chỉ được tăng tại thời điểm Checkout.

            await _unitOfWork.VoucherUsageLogs.DeleteAllByOrderIdAsync(orderId);

            // Reset order totals
            order.DiscountAmount = 0;
            order.TotalAmount = order.SubTotal + order.ShippingFee;

            await _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        public async Task<ApplicableVoucherResponse> GetApplicableVouchersAsync(Guid userId)
        {
            var response = new ApplicableVoucherResponse();

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            int userScore = user?.CurrentReputationScore ?? 0;
            bool canUseSystemVoucher = userScore >= _reputationSettings.CustomerHighMinScore;

            // 1. LẤY GIỎ HÀNG TỪ BẢNG MỚI
            var cart = await _unitOfWork.Carts.GetCartByUserIdAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                return response;

            // 2. TÍNH TOÁN SUBTOTAL CHO TỪNG SHOP VÀ TỔNG GIỎ HÀNG
            decimal cartSubTotal = 0;
            var shopSubTotals = new Dictionary<Guid, decimal>(); // Key: ShopId
            var userToShopMap = new Dictionary<Guid, Guid>(); // Key: UserId (CreatorId), Value: ShopId

            foreach (var item in cart.CartItems)
            {
                Guid shopId = Guid.Empty;
                decimal currentPrice = 0;

                // A. Hàng Custom Build
                if (item.IsCustom && item.ProductId.HasValue)
                {
                    var baseKit = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    if (baseKit != null)
                    {
                        shopId = baseKit.ShopId;
                        currentPrice = baseKit.Price;

                        // Giải mã JSON lấy giá linh kiện cộng dồn
                        if (!string.IsNullOrEmpty(item.DesignConfig))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(item.DesignConfig);
                                if (doc.RootElement.TryGetProperty("SelectedItemsJson", out var selectedItemsProp))
                                {
                                    var selectedItemsStr = selectedItemsProp.GetString();
                                    if (!string.IsNullOrEmpty(selectedItemsStr))
                                    {
                                        using var partsDoc = System.Text.Json.JsonDocument.Parse(selectedItemsStr);
                                        foreach (var part in partsDoc.RootElement.EnumerateObject())
                                        {
                                            var partObj = part.Value;
                                            decimal partPrice = 0;
                                            int partQty = 1;

                                            if (partObj.TryGetProperty("Price", out var priceProp)) partPrice = priceProp.GetDecimal();
                                            if (partObj.TryGetProperty("Quantity", out var qtyProp)) partQty = qtyProp.GetInt32();
                                            if (partQty <= 0) partQty = 1;

                                            currentPrice += (partPrice * partQty);
                                        }
                                    }
                                }
                            }
                            catch { /* Bỏ qua lỗi Parse */ }
                        }
                    }
                }
                // B. Hàng Thường (Assembled Product)
                else if (!item.IsCustom && item.AssembledProductId.HasValue)
                {
                    var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(item.AssembledProductId.Value);
                    if (assembledProduct != null)
                    {
                        currentPrice = assembledProduct.Price;

                        shopId = await _unitOfWork.Models.GetShopIdByAssembledProductAsync(assembledProduct.Id);
                    }
                }
                // C. Hàng Commission (Giữ nguyên luồng fallback cũ của dự án)
                else if (!string.IsNullOrEmpty(item.DesignConfig))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(item.DesignConfig);
                        if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var parsedShopId))
                            shopId = parsedShopId;

                        if (doc.RootElement.TryGetProperty("Price", out var priceProp) && priceProp.TryGetDecimal(out var parsedPrice))
                            currentPrice = parsedPrice;
                    }
                    catch { /* Ignore parse error */ }
                }

                decimal itemTotalPrice = currentPrice * item.Quantity;

                // Tích luỹ giá trị vào Shop và Tổng Giỏ Hàng
                if (shopId != Guid.Empty && itemTotalPrice > 0)
                {
                    if (!shopSubTotals.ContainsKey(shopId))
                    {
                        shopSubTotals[shopId] = 0;

                        // Lấy thông tin Shop để liên kết UserId (CreatorId của Voucher) với ShopId
                        var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
                        if (shop != null)
                        {
                            userToShopMap[shop.UserId] = shopId;
                        }
                    }

                    shopSubTotals[shopId] += itemTotalPrice;
                    cartSubTotal += itemTotalPrice;
                }
            }

            // 3. Get valid vouchers using existing Repository method
            var validVouchers = (await _unitOfWork.Vouchers.GetValidVouchersForUserAsync(userId)).ToList();

            // 4. Categorize and Check MinOrderValue
            var systemVouchers = new List<Domain.Entities.Voucher>();
            var shopVouchersDict = new Dictionary<Guid, List<Domain.Entities.Voucher>>();

            foreach (var v in validVouchers)
            {
                // Skip if usage limit is reached
                if (v.UsedCount >= v.UsageLimit) continue;
                if (v.MaxUsesPerUser.HasValue)
                {
                    // Đếm số lần user đã dùng mã này trong bảng VoucherUsageLogs
                    int userUsage = await _unitOfWork.VoucherUsageLogs.CountUsageByUserAndVoucherAsync(userId, v.Id, Guid.Empty);

                    // Nếu đã dùng bằng hoặc vượt quá số lượt cho phép -> Đá văng khỏi danh sách hiển thị
                    if (userUsage >= v.MaxUsesPerUser.Value)
                        continue;
                }
                // A. System/Platform Vouchers (Admin created OR Compensation)
                if (v.Scope == VoucherScope.System || v.Type == VoucherType.Compensation)
                {
                    if (v.Scope == VoucherScope.System && !canUseSystemVoucher)
                        continue;

                    // Check against TOTAL Cart value
                    if (cartSubTotal >= v.MinOrderValue)
                    {
                        systemVouchers.Add(v);
                    }
                }
                // B. Shop Specific Vouchers
                else if (v.Scope == VoucherScope.Shop && v.ShopId.HasValue)
                {
                    Guid shopIdOfVoucher = v.ShopId.Value;
                    if (shopSubTotals.ContainsKey(shopIdOfVoucher) && shopSubTotals[shopIdOfVoucher] >= v.MinOrderValue)
                    {
                        if (!shopVouchersDict.ContainsKey(shopIdOfVoucher))
                        {
                            shopVouchersDict[shopIdOfVoucher] = new List<Domain.Entities.Voucher>();
                        }

                        shopVouchersDict[shopIdOfVoucher].Add(v);
                    }
                }
            }

            // 5. Map to DTOs
            response.SystemVouchers = _mapper.Map<List<VoucherResponse>>(systemVouchers).ConvertDatesToLocal();

            foreach (var kvp in shopVouchersDict)
            {
                response.ShopVoucherGroups.Add(new ShopVoucherGroupResponse
                {
                    ShopId = kvp.Key,
                    Vouchers = _mapper.Map<List<VoucherResponse>>(kvp.Value).ConvertDatesToLocal()
                });
            }

            return response;
        }

        public async Task<List<AppliedVoucherResponse>> GetAppliedVouchersByOrderIdAsync(Guid orderId)
        {
            // Lấy danh sách record từ bảng cầu nối OrderVouchers
            var appliedVouchers = await _unitOfWork.VoucherUsageLogs.GetByOrderIdAsync(orderId);
            return _mapper.Map<List<AppliedVoucherResponse>>(appliedVouchers);
        }

        public async Task<PaginatedResult<VoucherUsageResponse>> GetVoucherUsageHistoryAsync(Guid userId, Guid voucherId, int pageNumber, int pageSize)
        {
            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(voucherId);
            if (voucher == null) throw new KeyNotFoundException("Voucher not found.");

            // Security Check: Lấy user hiện tại để kiểm tra quyền
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            // Chỉ cho phép Admin, hoặc chính Chủ shop đã tạo ra mã đó được quyền xem
            if (!isAdmin && voucher.CreatorId != userId)
            {
                throw new UnauthorizedAccessException("Access denied. You are not authorized to view this voucher’s statistics.");
            }

            var (items, totalCount) = await _unitOfWork.VoucherUsageLogs.GetUsageByVoucherIdAsync(voucherId, pageNumber, pageSize);

            var mappedItems = _mapper.Map<List<VoucherUsageResponse>>(items).ConvertDatesToLocal();
            return new PaginatedResult<VoucherUsageResponse>(mappedItems, totalCount, pageNumber, pageSize);
        }
        public async Task<PaginatedResult<VoucherUsageResponse>> GetAllVoucherUsagesAsync(Guid userId, int pageNumber, int pageSize)
        {
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            // Nếu Admin thì filterId = null (Lấy hết). Nếu Shop thì filterId = userId của shop.
            Guid? filterCreatorId = isAdmin ? null : userId;

            var (items, totalCount) = await _unitOfWork.VoucherUsageLogs.GetAllUsagesAsync(filterCreatorId, pageNumber, pageSize);

            var mappedItems = _mapper.Map<List<VoucherUsageResponse>>(items).ConvertDatesToLocal();
            return new PaginatedResult<VoucherUsageResponse>(mappedItems, totalCount, pageNumber, pageSize);
        }

        // ─── Private Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Validate stacking business rule: which voucher types can be combined.
        /// Rules:
        ///   Promotion  + Compensation  - Allowed
        ///   Negotiation + Compensation - Allowed
        ///   Promotion  + Promotion     - Not Allowed
        ///   Negotiation + Negotiation  - Not Allowed
        ///   Promotion  + Negotiation   - Not Allowed
        ///   Compensation + Compensation - Not Allowed
        /// </summary>
        //private static void ValidateStackingCombination(VoucherType existing, VoucherType incoming)
        //{
        //    bool allowed =
        //        (existing == VoucherType.Promotion && incoming == VoucherType.Compensation) ||
        //        (existing == VoucherType.Compensation && incoming == VoucherType.Promotion) ||
        //        (existing == VoucherType.Negotiation && incoming == VoucherType.Compensation) ||
        //        (existing == VoucherType.Compensation && incoming == VoucherType.Negotiation);

        //    if (!allowed)
        //        throw new Exception(
        //            $"Cannot combine a '{incoming}' voucher with an already-applied '{existing}' voucher. " +
        //            "Allowed stacking: Promotion/Negotiation + Compensation only.");
        //}

        private async Task ValidateVoucherScopeAsync(Voucher voucher, Order order)
        {
            if (voucher.Scope == VoucherScope.System) return;
            if (!order.ShopId.HasValue) return;

            if (order.Shop == null)
                order.Shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);

            if (order.Shop == null) return;

            var voucherCreator = await _unitOfWork.Users.GetByIdAsync(voucher.CreatorId);
            bool isShopOwner = voucher.CreatorId == order.Shop.UserId;
            bool isAdmin = voucherCreator?.Role?.Name == RoleType.Admin;

            switch (voucher.Type)
            {
                case VoucherType.Negotiation:
                    if (!isShopOwner)
                        throw new Exception("This negotiation voucher is not valid for this shop.");
                    break;
                case VoucherType.Promotion:
                    if (!isShopOwner && !isAdmin)
                        throw new Exception("This promotion voucher is not applicable for this shop's order.");
                    break;
                case VoucherType.Compensation:
                    break; // Always allowed
            }
        }

        public decimal CalculateVoucherDiscount(Voucher voucher, decimal subTotal)
        {
            decimal discount;
            if (voucher.DiscountType == DiscountType.FixedAmount)
            {
                discount = voucher.Value;
            }
            else
            {
                discount = subTotal * (voucher.Value / 100);
                if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
                    discount = voucher.MaxDiscountAmount.Value;
            }
            return Math.Min(discount, subTotal);
        }

        private static ApplyVoucherResult BuildApplyResult(Order order, List<VoucherUsageLog> appliedList)
        {
            return new ApplyVoucherResult
            {
                NewDiscountAmount = appliedList.LastOrDefault()?.DiscountApplied ?? 0,
                TotalDiscountAmount = order.DiscountAmount,
                FinalTotal = order.TotalAmount,
                AppliedVouchersCount = appliedList.Count,
                AppliedVouchers = appliedList.Select(ov => new AppliedVoucherDetail
                {
                    Code = ov.Code,
                    VoucherType = ov.VoucherType.ToString(),
                    DiscountApplied = ov.DiscountApplied,
                    ApplyOrder = ov.ApplyOrder
                }).ToList()
            };
        }

        // 7. Get list of valid vouchers for user
        public async Task<List<VoucherResponse>> GetMyVouchersAsync(Guid userId)
        {
            var vouchers = await _unitOfWork.Vouchers.GetValidVouchersForUserAsync(userId);
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            int userScore = user?.CurrentReputationScore ?? 0;
            bool canUseSystemVoucher = userScore >= _reputationSettings.CustomerHighMinScore;

            if (!canUseSystemVoucher)
            {
                vouchers = vouchers.Where(v => v.Scope != VoucherScope.System).ToList();
            }

            return _mapper.Map<List<VoucherResponse>>(vouchers).ConvertDatesToLocal();
        }

        public async Task<VoucherResponse> GetVoucherByIdAsync(Guid id)
        {
            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(id);
            if (voucher == null) throw new KeyNotFoundException("Voucher not found.");

            return _mapper.Map<VoucherResponse>(voucher).ConvertDatesToLocal();
        }

        public async Task<VoucherResponse> UpdateVoucherAsync(Guid userId, Guid voucherId, UpdateVoucherRequest request)
        {
            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(voucherId);
            if (voucher == null) throw new KeyNotFoundException("Voucher not found.");

            // Check permission: Only owner can edit
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            if (!isAdmin && voucher.CreatorId != userId)
                throw new UnauthorizedAccessException("You are not authorized to modify this voucher.");

            bool isUsed = await _unitOfWork.Vouchers.IsVoucherUsedAsync(voucherId);
            if (isUsed && (request.Value.HasValue || request.MaxDiscountAmount.HasValue || request.MinOrderValue.HasValue))
            {
                throw new InvalidOperationException("This voucher has already been used. Please delete it and create a new one instead of updating its value.");
            }
            // Update fields (only allow certain fields to be edited)
            if (!string.IsNullOrEmpty(request.Name)) voucher.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description)) voucher.Description = request.Description;
            if (request.Value.HasValue) voucher.Value = request.Value.Value;
            if (request.MaxDiscountAmount.HasValue) voucher.MaxDiscountAmount = request.MaxDiscountAmount.Value;
            if (request.MinOrderValue.HasValue) voucher.MinOrderValue = request.MinOrderValue.Value;
            if (request.EndDate.HasValue) voucher.EndDate = request.EndDate.Value;
            if (request.UsageLimit.HasValue)
            {
                // [P3-6] Không cho phép set UsageLimit nhỏ hơn số lượt đã dùng
                if (request.UsageLimit.Value < voucher.UsedCount)
                    throw new InvalidOperationException(
                        $"UsageLimit ({request.UsageLimit.Value}) cannot be less than the number of times already used ({voucher.UsedCount}).");
                voucher.UsageLimit = request.UsageLimit.Value;
            }
            if (request.MaxUsesPerUser.HasValue)
            {
                voucher.MaxUsesPerUser = request.MaxUsesPerUser.Value;
            }
            if (request.Status.HasValue) voucher.Status = request.Status.Value;

            _unitOfWork.Vouchers.Update(voucher);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<VoucherResponse>(voucher).ConvertDatesToLocal();
        }

        public async Task DeleteVoucherAsync(Guid userId, Guid voucherId)
        {
            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(voucherId);
            if (voucher == null) throw new KeyNotFoundException("Voucher not found.");
            // Check permission: Owner or Admin
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            if (!isAdmin && voucher.CreatorId != userId)
                throw new UnauthorizedAccessException("You are not authorized to modify this voucher.");

            voucher.Status = VoucherStatus.Expired;
            voucher.IsDeleted = true; // Soft delete only
            _unitOfWork.Vouchers.Update(voucher);
            await _unitOfWork.CommitAsync();
        }
        public async Task ToggleVoucherStatusAsync(Guid userId, Guid voucherId)
        {
            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(voucherId);
            if (voucher == null) throw new KeyNotFoundException("Voucher not found.");
            // Check permission: Owner or Admin
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            if (!isAdmin && voucher.CreatorId != userId)
                throw new UnauthorizedAccessException("You are not authorized to modify this voucher.");

            // [P3-5] Không cho phép tái kích hoạt voucher đã Expired hoặc Depleted
            if (voucher.Status == VoucherStatus.Expired || voucher.Status == VoucherStatus.Depleted)
                throw new InvalidOperationException(
                    $"Cannot toggle status of a voucher with status '{voucher.Status}'. " +
                    "Only Active and Disabled vouchers can be toggled.");

            // Đảo trạng thái Active <-> Inactive
            voucher.Status = voucher.Status == VoucherStatus.Active ? VoucherStatus.Disabled : VoucherStatus.Active;

            _unitOfWork.Vouchers.Update(voucher);
            await _unitOfWork.CommitAsync();
        }

        public async Task<List<VoucherResponse>> GetShopPublicVouchersAsync(Guid shopId)
        {
            // shopId ở đây là ID của ShopProfile
            // Cần lấy UserId của chủ shop đó trước
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Gọi Repo lấy voucher công khai của shopOwner (shop.UserId)
            var vouchers = await _unitOfWork.Vouchers.GetPublicVouchersByShopAsync(shop.UserId);
            return _mapper.Map<List<VoucherResponse>>(vouchers).ConvertDatesToLocal();
        }

        public async Task<PaginatedResult<VoucherResponse>> GetVouchersByShopAsync(Guid userId, VoucherFilterRequest filter)
        {
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            // Nếu là Admin -> Truyền null xuống Repo để lấy toàn bộ mã
            // Nếu là Shop -> Truyền ID thật của Shop để giới hạn dữ liệu
            Guid? targetCreatorId = isAdmin ? null : userId;

            var (items, totalCount) = await _unitOfWork.Vouchers.GetVouchersByFilterAsync(targetCreatorId, filter);

            var mappedItems = _mapper.Map<List<VoucherResponse>>(items).ConvertDatesToLocal();
            return new PaginatedResult<VoucherResponse>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);
        }

    }
}
