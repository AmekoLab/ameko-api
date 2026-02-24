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

        // 1. Create Promotional Voucher (Marketing)
        public async Task<VoucherResponse> CreatePromotionalVoucherAsync(Guid userId, CreateVoucherRequest request)
        {
            // Validate: Code must be unique
            var existing = await _unitOfWork.Vouchers.GetByCodeAsync(request.Code);
            if (existing != null)
                throw new Exception($"Voucher code '{request.Code}' already exists.");

            var voucher = _mapper.Map<Voucher>(request);
            voucher.CreatorId = userId;

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
                EndDate = DateTime.Now.AddDays(7), // Expires in 7 days to close the deal
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
                EndDate = DateTime.Now.AddMonths(1), // Expires in 1 month
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

            if (appliedList.Count >= 2)
                throw new Exception("Maximum 2 vouchers can be applied to one order.");

            if (appliedList.Count == 1)
            {
                var existingVoucher = await _unitOfWork.Vouchers.GetByIdAsync(appliedList[0].VoucherId);
                if (existingVoucher == null || !existingVoucher.IsStackable || !incomingVoucher.IsStackable)
                    throw new Exception("One of the applied vouchers does not allow stacking.");

                ValidateStackingCombination(existingVoucher.Type, incomingVoucher.Type);
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

            // Lấy danh sách các món hàng trong giỏ
            var cartItems = order.OrderItems.Where(i => !i.IsDeleted).ToList();

            for (int i = 0; i < allVouchersToApply.Count; i++)
            {
                var currentVoucher = allVouchersToApply[i];

                // Mặc định: Lấy tổng giỏ hàng (Áp dụng cho mã Sàn/Đền bù)
                decimal baseCalculationAmount = order.SubTotal;

                // KIỂM TRA NẾU ĐÂY LÀ MÃ CỦA SHOP
                if (currentVoucher.CreatorId != Guid.Empty &&
                    (currentVoucher.Type == VoucherType.Promotion || currentVoucher.Type == VoucherType.Negotiation))
                {
                    decimal shopSubTotal = 0;
                    // Tính tổng tiền của ĐÚNG CÁC MÓN HÀNG thuộc Shop đó
                    foreach (var item in cartItems)
                    {
                        var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                        if (product != null && product.ShopId == currentVoucher.CreatorId)
                        {
                            shopSubTotal += item.TotalPrice;
                        }
                    }

                    baseCalculationAmount = shopSubTotal;

                    // Chặn: Nếu tổng tiền hàng của riêng Shop này chưa đủ điều kiện
                    if (baseCalculationAmount < currentVoucher.MinOrderValue)
                    {
                        throw new Exception($"Tổng tiền các sản phẩm của Shop chưa đạt mức tối thiểu {currentVoucher.MinOrderValue:N0} VND để dùng mã {currentVoucher.Code}.");
                    }
                }
                else
                {
                    // Mã Hệ Thống (Sàn/Đền bù): Kiểm tra trên tổng giỏ hàng
                    if (baseCalculationAmount < currentVoucher.MinOrderValue)
                    {
                        throw new Exception($"Đơn hàng chưa đạt mức tối thiểu {currentVoucher.MinOrderValue:N0} VND để dùng mã {currentVoucher.Code}.");
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
                        decimal shopSubTotal = 0;
                        foreach (var item in cartItems)
                        {
                            var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                            if (product != null && product.ShopId == underlyingVoucher.CreatorId)
                            {
                                shopSubTotal += item.TotalPrice;
                            }
                        }
                        baseCalculationAmount = shopSubTotal;
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
            var shopSubTotals = new Dictionary<Guid, decimal>();
            var cartItems = cartOrder.OrderItems.Where(i => !i.IsDeleted).ToList();

            foreach (var item in cartItems)
            {
                var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
                if (product != null && product.ShopId != Guid.Empty)
                {
                    var shopId = product.ShopId;
                    if (!shopSubTotals.ContainsKey(shopId)) shopSubTotals[shopId] = 0;

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
                if (v.CreatorId == Guid.Empty || v.Type == VoucherType.Compensation)
                {
                    // Check against TOTAL Cart value
                    if (cartSubTotal >= v.MinOrderValue)
                    {
                        systemVouchers.Add(v);
                    }
                }
                // B. Shop Specific Vouchers
                else if (v.CreatorId != Guid.Empty && shopSubTotals.ContainsKey(v.CreatorId))
                {
                    // Check against SPECIFIC Shop SubTotal
                    if (shopSubTotals[v.CreatorId] >= v.MinOrderValue)
                    {
                        if (!shopVouchersDict.ContainsKey(v.CreatorId))
                            shopVouchersDict[v.CreatorId] = new List<Domain.Entities.Voucher>();

                        shopVouchersDict[v.CreatorId].Add(v);
                    }
                }
            }

            // 5. Map to DTOs
            response.SystemVouchers = _mapper.Map<List<VoucherResponse>>(systemVouchers);

            foreach (var kvp in shopVouchersDict)
            {
                response.ShopVoucherGroups.Add(new ShopVoucherGroupResponse
                {
                    ShopId = kvp.Key,
                    Vouchers = _mapper.Map<List<VoucherResponse>>(kvp.Value)
                });
            }

            return response;
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
        private static void ValidateStackingCombination(VoucherType existing, VoucherType incoming)
        {
            bool allowed =
                (existing == VoucherType.Promotion && incoming == VoucherType.Compensation) ||
                (existing == VoucherType.Compensation && incoming == VoucherType.Promotion) ||
                (existing == VoucherType.Negotiation && incoming == VoucherType.Compensation) ||
                (existing == VoucherType.Compensation && incoming == VoucherType.Negotiation);

            if (!allowed)
                throw new Exception(
                    $"Cannot combine a '{incoming}' voucher with an already-applied '{existing}' voucher. " +
                    "Allowed stacking: Promotion/Negotiation + Compensation only.");
        }

        private async Task ValidateVoucherScopeAsync(Voucher voucher, Order order)
        {
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
            if (voucher.CreatorId != userId) throw new UnauthorizedAccessException("You are not the owner of this voucher.");

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
            if (voucher.CreatorId != userId) throw new UnauthorizedAccessException("You are not the owner of this voucher.");

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
