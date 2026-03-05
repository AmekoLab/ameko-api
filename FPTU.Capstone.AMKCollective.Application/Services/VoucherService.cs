using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
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

        public VoucherService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // 1. Tạo Voucher Khuyến mãi (Marketing)
        public async Task<VoucherResponse> CreatePromotionalVoucherAsync(Guid userId, CreateVoucherRequest request)
        {
            // Validate: Code phải duy nhất
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

            // Logic mặc định cho Promotion
            voucher.Status = VoucherStatus.Active;
            voucher.UsedCount = 0;

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            var creator = await _unitOfWork.Users.GetByIdAsync(userId);
            voucher.Creator = creator;

            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 2. Tạo Voucher Thương lượng (Negotiation) - Dành cho Shop chốt deal
        public async Task<VoucherResponse> CreateNegotiationVoucherAsync(Guid shopId, Guid targetUserId, decimal discountAmount, decimal minOrderValue)
        {
            // Sinh mã ngẫu nhiên: NEGO_ + 8 ký tự random
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
                MinOrderValue = minOrderValue, // Ràng buộc: Phải mua đủ số tiền đã chốt

                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7), // Hạn 7 ngày để chốt đơn
                UsageLimit = 1,
                UsedCount = 0,
                Status = VoucherStatus.Active,

                CreatorId = shopId,
                TargetUserId = targetUserId // Chỉ khách này dùng được
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            var creator = await _unitOfWork.Users.GetByIdAsync(shopId); 
            voucher.Creator = creator;
            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 3. Tạo Voucher Đền bù (Compensation/Refund) - Dành cho System/Shop hủy đơn
        public async Task<VoucherResponse> CreateCompensationVoucherAsync(Guid shopId, Guid targetUserId, decimal refundAmount)
        {
            // Sinh mã: REFUND_ + ...
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
                MinOrderValue = 0, // Không cần đơn tối thiểu, dùng cho đơn nào cũng được

                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(1), // Hạn 1 tháng (theo yêu cầu)
                UsageLimit = 1,
                UsedCount = 0,
                Status = VoucherStatus.Active,

                CreatorId = shopId, // Shop chịu trách nhiệm tạo (hoặc Admin)
                TargetUserId = targetUserId
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 4. Áp dụng Voucher vào Đơn hàng 
        public async Task<decimal> ApplyVoucherAsync(Guid userId, Guid orderId, string code)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");

            // Kiểm tra chủ sở hữu đơn hàng
            if (order.CustomerId != userId) throw new Exception("Unauthorized to modify this order.");

            // Lấy Voucher
            var voucher = await _unitOfWork.Vouchers.GetByCodeAsync(code);
            if (voucher == null) throw new Exception("Voucher not found.");

            // --- VALIDATION LOGIC ---

            // 1. Check Status & Date
            if (voucher.Status != VoucherStatus.Active) throw new Exception("Voucher is not active.");
            if (DateTime.Now < voucher.StartDate || DateTime.Now > voucher.EndDate) throw new Exception("Voucher is expired or not yet started.");

            // 2. Check Usage Limit
            if (voucher.UsedCount >= voucher.UsageLimit) throw new Exception("Voucher usage limit reached.");

            // 3. Check Target User (Quyền riêng tư)
            if (voucher.TargetUserId != null && voucher.TargetUserId != userId)
                throw new Exception("This voucher is not applicable to you.");

            // 4. Check Scope (Validate quyền Shop Owner vs Admin Global)
            if (order.ShopId.HasValue)
            {
                // Load Shop Profile để lấy UserId của chủ shop
                if (order.Shop == null)
                {
                    order.Shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                }

                if (order.Shop != null)
                {
                    // Lấy thông tin người tạo Voucher (check xem là Admin hay Shop)
                    var voucherCreator = await _unitOfWork.Users.GetByIdAsync(voucher.CreatorId);

                    // Logic check:
                    // - Là Shop Owner: CreatorId trùng với UserId của Shop
                    // - Là Admin: Role của Creator là Admin
                    bool isShopOwner = voucher.CreatorId == order.Shop.UserId;

                    // Nếu voucherCreator load lên bị null hoặc Role null thì mặc định false
                    bool isAdmin = voucherCreator != null && voucherCreator.Role != null && voucherCreator.Role.Name == RoleType.Admin;

                    switch (voucher.Type)
                    {
                        case VoucherType.Negotiation:
                            // Voucher thương lượng: Bắt buộc Shop phải tự tạo cho khách
                            if (!isShopOwner)
                                throw new Exception("This negotiation voucher is not valid for this shop.");
                            break;

                        case VoucherType.Promotion:
                            // Voucher khuyến mãi:
                            // - Nếu Shop tạo: Chỉ áp dụng cho Shop đó.
                            // - Nếu Admin tạo: Áp dụng được (Global).
                            if (!isShopOwner && !isAdmin)
                                throw new Exception("This promotion voucher is not applicable for this shop's order.");
                            break;

                        case VoucherType.Compensation:
                            // Voucher đền bù: Thường do hệ thống/Admin tạo
                            // Luôn cho phép áp dụng (Global)
                            break;
                    }
                }
            }

            // 5. Check Min Order Value
            if (order.SubTotal < voucher.MinOrderValue)
                throw new Exception($"Order value must be at least {voucher.MinOrderValue:N0} VND to use this voucher.");

            // --- CALCULATE DISCOUNT ---
            decimal discount = 0;
            if (voucher.DiscountType == DiscountType.FixedAmount)
            {
                discount = voucher.Value;
            }
            else // Percentage
            {
                discount = order.SubTotal * (voucher.Value / 100);
                if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
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

            // Đảm bảo không giảm quá giá trị đơn hàng (tránh âm tiền)
            if (discount > order.SubTotal) discount = order.SubTotal;

            // --- UPDATE ORDER ---
            // Lưu ý: Logic này sẽ GHI ĐÈ voucher cũ nếu có (chưa stacking)
            order.VoucherId = voucher.Id;
            order.DiscountAmount = discount;
            order.TotalAmount = (order.SubTotal + order.ShippingFee) - discount;

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            return discount;
        }

        // 5. Hủy áp dụng Voucher (Remove)
        public async Task RemoveVoucherAsync(Guid userId, Guid orderId)
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

                    if (underlyingVoucher.CreatorId != Guid.Empty &&
                       (underlyingVoucher.Type == VoucherType.Promotion || underlyingVoucher.Type == VoucherType.Negotiation))
                    {
                        // Lấy tổng tiền đã tính sẵn cho chủ Shop này
                        baseCalculationAmount = creatorSubTotals.ContainsKey(underlyingVoucher.CreatorId.Value)
                                                ? creatorSubTotals[underlyingVoucher.CreatorId.Value]
                                                : 0;
                    }

                    decimal stepDiscount = CalculateVoucherDiscount(underlyingVoucher, baseCalculationAmount);

                    if (underlyingVoucher.Type == VoucherType.Promotion || underlyingVoucher.Type == VoucherType.Negotiation)
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

            // shopVouchersDict vẫn dùng Key là ShopId để trả về response cho chuẩn
            var shopVouchersDict = new Dictionary<Guid, List<Domain.Entities.Voucher>>();

            foreach (var v in validVouchers)
            {
                // Skip if usage limit is reached
                if (v.UsedCount >= v.UsageLimit) continue;

                // A. System/Platform Vouchers (Admin created OR Compensation)
                if (v.CreatorId == Guid.Empty || v.Type == VoucherType.Compensation)
                {
                    // Check against TOTAL Cart value
                    if (cartSubTotal >= v.MinOrderValue)
                    {
                        systemVouchers.Add(v);
                    }
                }
                // B. Shop Specific Vouchers
                // Kiểm tra xem CreatorId (UserId) của voucher có khớp với Shop nào trong giỏ hàng không
                else if (v.CreatorId != Guid.Empty && userToShopMap.TryGetValue(v.CreatorId.Value, out var mappedShopId))
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

            // Check quyền: Chỉ chủ sở hữu mới được sửa
            if (voucher.CreatorId != userId) throw new UnauthorizedAccessException("You are not the owner of this voucher.");

            // Update fields (Chỉ cho phép sửa một số trường nhất định)
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
            if (voucher.CreatorId != userId) throw new UnauthorizedAccessException("You are not the owner of this voucher.");

            // CHECK AN TOÀN: Nếu voucher đã có người dùng -> Không được xóa, bắt buộc phải Deactivate
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
            if (voucher.CreatorId != userId) throw new UnauthorizedAccessException("Unauthorized.");

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
