using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
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

        public VoucherService(IUnitOfWork unitOfWork, IMapper mapper, IOptions<VoucherSettings> voucherOptions)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _voucherSettings = voucherOptions.Value;
        }

        // 1. Create Promotional Voucher (Marketing)
        public async Task<VoucherResponse> CreatePromotionalVoucherAsync(Guid userId, CreateVoucherRequest request)
        {
            // Validate: Code must be unique
            var existing = await _unitOfWork.Vouchers.GetByCodeAsync(request.Code);
            if (existing != null)
                throw new Exception($"Voucher code '{request.Code}' already exists.");

            var voucher = _mapper.Map<Voucher>(request);
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            if (isAdmin)
            {
                // Nếu là Admin tạo -> Ép CreatorId = null để hệ thống nhận diện đây là Mã Sàn
                voucher.CreatorId = null;
            }
            else
            {
                // Nếu là Shop tạo -> Lưu ID của Shop để khóa dòng tiền cho riêng Shop đó
                voucher.CreatorId = userId;
            }

            // ---> FIX 2: ÉP CỨNG LOẠI VOUCHER CHỐNG GIAN LẬN <---
            // Bất kể Front-end truyền lên Type là gì, API này chỉ được phép tạo mã Promotion
            voucher.Type = VoucherType.Promotion;

            // Default logic for Promotion
            voucher.Status = VoucherStatus.Active;
            voucher.UsedCount = 0;

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            var creator = await _unitOfWork.Users.GetByIdAsync(userId);
            voucher.Creator = creator;

            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 2. Create Negotiation Voucher - For shop to finalize deals
        public async Task<VoucherResponse> CreateNegotiationVoucherAsync(Guid shopId, Guid targetUserId, decimal discountAmount, decimal minOrderValue)
        {
            // Generate random code: NEGO_ + 8 random characters
            string code = "NEGO_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

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

                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(_voucherSettings.NegotiationValidityDays), // Expires in 7 days to close the deal
                UsageLimit = 1,
                UsedCount = 0,
                Status = VoucherStatus.Active,

                CreatorId = shopId,
                TargetUserId = targetUserId, // Only this customer can use

                // Negotiation: can only stack with Compensation voucher
                IsStackable = true,
                StackingPolicy = StackingPolicy.WithCompensationOnly
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            var creator = await _unitOfWork.Users.GetByIdAsync(shopId); 
            voucher.Creator = creator;
            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 3. Create Compensation Voucher - For system/shop cancellations
        public async Task<VoucherResponse> CreateCompensationVoucherAsync(Guid shopId, Guid targetUserId, decimal refundAmount)
        {
            // Generate code: REFUND_ + ...
            string code = "REFUND_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

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

                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(_voucherSettings.CompensationValidityMonths), 
                UsageLimit = 1,
                UsedCount = 0,
                Status = VoucherStatus.Active,

                CreatorId = shopId, // Shop or Admin responsible for creation
                TargetUserId = targetUserId,

                // Compensation: can stack with all other voucher types
                IsStackable = true,
                StackingPolicy = StackingPolicy.All
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 4. Apply Voucher to Order (Stacking)
        public async Task<ApplyVoucherResult> ApplyVoucherAsync(Guid userId, Guid orderId, string code)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized to modify this order.");

            var incomingVoucher = await _unitOfWork.Vouchers.GetByCodeAsync(code);
            if (incomingVoucher == null) throw new Exception("Voucher not found.");

            // --- BASIC VALIDATION ---
            if (incomingVoucher.Status != VoucherStatus.Active) throw new Exception("Voucher is not active.");
            if (DateTime.Now < incomingVoucher.StartDate || DateTime.Now > incomingVoucher.EndDate) throw new Exception("Voucher is expired or not yet started.");
            if (incomingVoucher.UsedCount >= incomingVoucher.UsageLimit) throw new Exception("Voucher usage limit reached.");
            if (incomingVoucher.TargetUserId != null && incomingVoucher.TargetUserId != userId)
                throw new Exception("This voucher is not applicable to you.");

            // --- STACKING VALIDATION ---
            var appliedList = (await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId)).ToList();

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
            bool isIncomingSystemPromo = incomingVoucher.CreatorId == null && incomingVoucher.Type == VoucherType.Promotion; 
            bool isIncomingShopVoucher = incomingVoucher.CreatorId != null && 
                                         (incomingVoucher.Type == VoucherType.Promotion || incomingVoucher.Type == VoucherType.Negotiation);

            // 3. KIỂM TRA GIỚI HẠN, HỖ TRỢ MULTI-SHOP
            if (!isIncomingCompensation)
            {
                if (isIncomingSystemPromo)
                {
                    // Chỉ được 1 mã của Sàn cho toàn bộ giỏ hàng
                    if (existingVouchers.Any(v => v.CreatorId == null && v.Type == VoucherType.Promotion))
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
                    bool hasItemFromThisShop = order.OrderItems.Any(i =>
                    {
                        Guid sId = Guid.Empty;
                        if (i.ProductId.HasValue)
                        {
                            var productTask = _unitOfWork.Models.GetByIdAsync(i.ProductId.Value).Result;
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
                            var shopTask = _unitOfWork.Shops.GetByIdAsync(sId).Result;
                            return shopTask != null && shopTask.UserId == incomingVoucher.CreatorId;
                        }
                        return false;
                    });

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

            await _unitOfWork.OrderVouchers.DeleteAllByOrderIdAsync(orderId);

            // --- BẮT ĐẦU CÔ LẬP SỐ TIỀN (ISOLATION CALCULATION) ---
            decimal totalDiscountAmount = 0;
            var newAppliedList = new List<OrderVoucher>();

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
                if (currentVoucher.CreatorId != null &&
                    (currentVoucher.Type == VoucherType.Promotion || currentVoucher.Type == VoucherType.Negotiation))
                {
                    // Lấy ngay tổng tiền đã tính sẵn từ Dictionary ra
                    baseCalculationAmount = creatorSubTotals.ContainsKey(currentVoucher.CreatorId.Value)
                                            ? creatorSubTotals[currentVoucher.CreatorId.Value]
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
                var orderVoucher = new OrderVoucher
                {
                    OrderId = orderId,
                    VoucherId = currentVoucher.Id,
                    VoucherCode = currentVoucher.Code,
                    VoucherType = currentVoucher.Type,
                    DiscountApplied = stepDiscount,
                    ApplyOrder = i + 1
                };

                await _unitOfWork.OrderVouchers.AddAsync(orderVoucher);
                newAppliedList.Add(orderVoucher);

                totalDiscountAmount += stepDiscount;

                // Chỉ tăng UsedCount cho mã vừa nhập
                if (currentVoucher.Id == incomingVoucher.Id)
                {
                    currentVoucher.UsedCount++;
                    _unitOfWork.Vouchers.Update(currentVoucher);
                }
            }

            // --- UPDATE ORDER ---
            order.DiscountAmount = totalDiscountAmount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscountAmount);
            order.VoucherId = null;

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            return BuildApplyResult(order, newAppliedList);
        }

        // 5. Remove a specific voucher from order
        public async Task<ApplyVoucherResult> RemoveSpecificVoucherAsync(Guid userId, Guid orderId, string voucherCode)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized.");

            var voucherToRemove = await _unitOfWork.Vouchers.GetByCodeAsync(voucherCode);
            if (voucherToRemove == null) throw new Exception("Voucher not found.");

            var orderVoucherToRemove = await _unitOfWork.OrderVouchers.GetByOrderAndVoucherAsync(orderId, voucherToRemove.Id);
            if (orderVoucherToRemove == null) throw new Exception("This voucher is not applied to the order.");

            // Remove the bridge record
            _unitOfWork.OrderVouchers.Delete(orderVoucherToRemove);

            // Restore UsedCount for the removed voucher
            if (voucherToRemove.UsedCount > 0)
            {
                voucherToRemove.UsedCount--;
                _unitOfWork.Vouchers.Update(voucherToRemove);
            }

            // Recalculate remaining vouchers to ensure caps are still accurate
            var remainingOrderVouchers = (await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId))
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

                    if (underlyingVoucher.CreatorId != null &&
                       (underlyingVoucher.Type == VoucherType.Promotion || underlyingVoucher.Type == VoucherType.Negotiation))
                    {
                        // Lấy tổng tiền đã tính sẵn cho chủ Shop này
                        baseCalculationAmount = creatorSubTotals.ContainsKey(underlyingVoucher.CreatorId.Value)
                                                ? creatorSubTotals[underlyingVoucher.CreatorId.Value]
                                                : 0;
                    }

                    decimal stepDiscount = CalculateVoucherDiscount(underlyingVoucher, baseCalculationAmount);

                    if (underlyingVoucher.CreatorId != null &&
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
                    _unitOfWork.OrderVouchers.Update(ov);

                    totalDiscountAmount += stepDiscount;
                }
            }

            // Update Order totals
            order.DiscountAmount = totalDiscountAmount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscountAmount);

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            return BuildApplyResult(order, remainingOrderVouchers);
        }

        // 6. Remove all vouchers from order
        public async Task RemoveAllVouchersAsync(Guid userId, Guid orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized.");

            var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId);

            // Restore UsedCount for ALL applied vouchers before deleting
            foreach (var av in appliedVouchers)
            {
                var v = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                if (v != null && v.UsedCount > 0)
                {
                    v.UsedCount--;
                    _unitOfWork.Vouchers.Update(v);
                }
            }

            await _unitOfWork.OrderVouchers.DeleteAllByOrderIdAsync(orderId);

            // Reset order totals
            order.VoucherId = null;
            order.DiscountAmount = 0;
            order.TotalAmount = order.SubTotal + order.ShippingFee;

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        public async Task<ApplicableVoucherResponse> GetApplicableVouchersAsync(Guid userId)
        {
            var response = new ApplicableVoucherResponse();

            // 1. Get current cart
            var cartOrder = await _unitOfWork.Orders.GetOrderByStatusAsync(userId, OrderStatus.InCart);
            if (cartOrder == null || !cartOrder.OrderItems.Any())
                return response;

            // 2. Calculate SubTotal for each Shop and Total Cart
            decimal cartSubTotal = 0;
            var shopSubTotals = new Dictionary<Guid, decimal>(); // Key: ShopId

            // Dictionary để map giữa UserId của chủ shop và ShopId
            var userToShopMap = new Dictionary<Guid, Guid>(); // Key: UserId (CreatorId), Value: ShopId

            var cartItems = cartOrder.OrderItems.Where(i => !i.IsDeleted).ToList();

            foreach (var item in cartItems)
            {
                Guid shopId = Guid.Empty;

                // 1. Hàng thường & Builder (Có ProductId)
                if (item.ProductId.HasValue)
                {
                    var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId.Value);
                    if (product != null) shopId = product.ShopId;
                }
                // 2. Hàng Commission (Lấy ShopId từ DesignConfig)
                else if (!string.IsNullOrEmpty(item.DesignConfig))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(item.DesignConfig);
                        if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var parsedShopId))
                            shopId = parsedShopId;
                    }
                    catch { /* Ignore parse error */ }
                }

                // Tính tổng tiền cho Shop
                if (shopId != Guid.Empty)
                {
                    if (!shopSubTotals.ContainsKey(shopId))
                    {
                        shopSubTotals[shopId] = 0;

                        // Lấy thông tin Shop để liên kết UserId với ShopId
                        var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
                        if (shop != null)
                        {
                            userToShopMap[shop.UserId] = shopId;
                        }
                    }

                    shopSubTotals[shopId] += item.TotalPrice;
                    cartSubTotal += item.TotalPrice;
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

                // A. System/Platform Vouchers (Admin created OR Compensation)
                if (v.CreatorId == null || v.Type == VoucherType.Compensation)
                {
                    // Check against TOTAL Cart value
                    if (cartSubTotal >= v.MinOrderValue)
                    {
                        systemVouchers.Add(v);
                    }
                }
                // B. Shop Specific Vouchers
                else if (v.CreatorId.HasValue && userToShopMap.TryGetValue(v.CreatorId.Value, out var mappedShopId))
                {
                    // Check against SPECIFIC Shop SubTotal (sử dụng mappedShopId)
                    if (shopSubTotals[mappedShopId] >= v.MinOrderValue)
                    {
                        if (!shopVouchersDict.ContainsKey(mappedShopId))
                            shopVouchersDict[mappedShopId] = new List<Domain.Entities.Voucher>();

                        shopVouchersDict[mappedShopId].Add(v);
                    }
                }
            }

            // 5. Map to DTOs
            response.SystemVouchers = _mapper.Map<List<VoucherResponse>>(systemVouchers);

            foreach (var kvp in shopVouchersDict)
            {
                response.ShopVoucherGroups.Add(new ShopVoucherGroupResponse
                {
                    ShopId = kvp.Key, // kvp.Key lúc này chính xác là ShopId
                    Vouchers = _mapper.Map<List<VoucherResponse>>(kvp.Value)
                });
            }

            return response;
        }

        public async Task<List<AppliedVoucherResponse>> GetAppliedVouchersByOrderIdAsync(Guid orderId)
        {
            // Lấy danh sách record từ bảng cầu nối OrderVouchers
            var appliedVouchers = await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId);
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

            var (items, totalCount) = await _unitOfWork.OrderVouchers.GetUsageByVoucherIdAsync(voucherId, pageNumber, pageSize);

            var mappedItems = _mapper.Map<List<VoucherUsageResponse>>(items);
            return new PaginatedResult<VoucherUsageResponse>(mappedItems, totalCount, pageNumber, pageSize);
        }
        public async Task<PaginatedResult<VoucherUsageResponse>> GetAllVoucherUsagesAsync(Guid userId, int pageNumber, int pageSize)
        {
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            bool isAdmin = currentUser?.Role?.Name == RoleType.Admin;

            // Nếu Admin thì filterId = null (Lấy hết). Nếu Shop thì filterId = userId của shop.
            Guid? filterCreatorId = isAdmin ? null : userId;

            var (items, totalCount) = await _unitOfWork.OrderVouchers.GetAllUsagesAsync(filterCreatorId, pageNumber, pageSize);

            var mappedItems = _mapper.Map<List<VoucherUsageResponse>>(items);
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
            if (voucher.CreatorId == null) return;
            if (!order.ShopId.HasValue) return;

            if (order.Shop == null)
                order.Shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);

            if (order.Shop == null) return;

            var voucherCreator = await _unitOfWork.Users.GetByIdAsync(voucher.CreatorId.Value);
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

        private static ApplyVoucherResult BuildApplyResult(Order order, List<OrderVoucher> appliedList)
        {
            return new ApplyVoucherResult
            {
                NewDiscountAmount = appliedList.LastOrDefault()?.DiscountApplied ?? 0,
                TotalDiscountAmount = order.DiscountAmount,
                FinalTotal = order.TotalAmount,
                AppliedVouchersCount = appliedList.Count,
                AppliedVouchers = appliedList.Select(ov => new AppliedVoucherDetail
                {
                    Code = ov.VoucherCode,
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
            return _mapper.Map<List<VoucherResponse>>(vouchers);
        }

        public async Task<VoucherResponse> GetVoucherByIdAsync(Guid id)
        {
            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(id);
            if (voucher == null) throw new KeyNotFoundException("Voucher not found.");

            return _mapper.Map<VoucherResponse>(voucher);
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
            // Update fields (only allow certain fields to be edited)
            if (!string.IsNullOrEmpty(request.Name)) voucher.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description)) voucher.Description = request.Description;
            if (request.EndDate.HasValue) voucher.EndDate = request.EndDate.Value;
            if (request.UsageLimit.HasValue) voucher.UsageLimit = request.UsageLimit.Value;
            if (request.Status.HasValue) voucher.Status = request.Status.Value;

            _unitOfWork.Vouchers.Update(voucher);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<VoucherResponse>(voucher);
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

            // SAFETY CHECK: If voucher already used by customers, cannot delete - must deactivate instead
            bool isUsed = await _unitOfWork.Vouchers.IsVoucherUsedAsync(voucherId);
            if (isUsed)
            {
                throw new InvalidOperationException("Cannot delete this voucher because it has been used by customers. Please deactivate it instead.");
            }

            _unitOfWork.Vouchers.Delete(voucher); // Soft Delete
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
            return _mapper.Map<List<VoucherResponse>>(vouchers);
        }

        public async Task<PaginatedResult<VoucherResponse>> GetVouchersByShopAsync(Guid userId, VoucherFilterRequest filter)
        {
            // userId ở đây là ID của User (Chủ Shop)
            var (items, totalCount) = await _unitOfWork.Vouchers.GetVouchersByFilterAsync(userId, filter);

            var mappedItems = _mapper.Map<List<VoucherResponse>>(items);
            return new PaginatedResult<VoucherResponse>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);
        }

    }
}
