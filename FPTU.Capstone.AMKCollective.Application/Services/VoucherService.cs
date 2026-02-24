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

        // 4. Apply Voucher to Order (Stacking - max 2 vouchers)
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
            if (order.SubTotal < incomingVoucher.MinOrderValue)
                throw new Exception($"Order value must be at least {incomingVoucher.MinOrderValue:N0} VND to use this voucher.");

            // --- VALIDATE SCOPE (Shop Owner vs Admin) ---
            await ValidateVoucherScopeAsync(incomingVoucher, order);

            // --- STACKING VALIDATION ---
            var appliedList = (await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId)).ToList();

            // Check for duplicate voucher
            if (appliedList.Any(av => av.VoucherId == incomingVoucher.Id))
                throw new Exception("This voucher has already been applied to this order.");

            // Business rule: maximum 2 vouchers per order
            if (appliedList.Count >= 2)
                throw new Exception("Maximum 2 vouchers can be applied to one order.");

            // [FIX 3]: Check IsStackable flag and validation rules
            if (appliedList.Count == 1)
            {
                var existingVoucher = await _unitOfWork.Vouchers.GetByIdAsync(appliedList[0].VoucherId);
                if (existingVoucher == null || !existingVoucher.IsStackable || !incomingVoucher.IsStackable)
                    throw new Exception("One of the applied vouchers does not allow stacking.");

                ValidateStackingCombination(existingVoucher.Type, incomingVoucher.Type);
            }

            // --- PREPARE CALCULATION PIPELINE ---
            // Gather all vouchers and calculate from scratch to ensure correct discount caps
            var allVouchersToApply = new List<Voucher>();
            foreach (var av in appliedList)
            {
                var v = await _unitOfWork.Vouchers.GetByIdAsync(av.VoucherId);
                if (v != null) allVouchersToApply.Add(v);
            }
            allVouchersToApply.Add(incomingVoucher);

            // Force ordering: Promotion/Negotiation first (0), Compensation last (1)
            allVouchersToApply = allVouchersToApply.OrderBy(v => v.Type == VoucherType.Compensation ? 1 : 0).ToList();

            // Clear old records to rewrite the exact applied amount
            await _unitOfWork.OrderVouchers.DeleteAllByOrderIdAsync(orderId);

            // --- CALCULATE DISCOUNT ---
            decimal totalDiscountAmount = 0;
            var newAppliedList = new List<OrderVoucher>();

            for (int i = 0; i < allVouchersToApply.Count; i++)
            {
                var currentVoucher = allVouchersToApply[i];

                // Calculate discount based on original SubTotal
                decimal stepDiscount = CalculateVoucherDiscount(currentVoucher, order.SubTotal);

                // Cap: Prevent discounting more than the remaining order value
                if (stepDiscount > (order.SubTotal - totalDiscountAmount))
                {
                    stepDiscount = order.SubTotal - totalDiscountAmount;
                }

                // Create new OrderVoucher tracking record
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

                // Increment UsedCount ONLY for the newly applied voucher
                if (currentVoucher.Id == incomingVoucher.Id)
                {
                    currentVoucher.UsedCount++;
                    _unitOfWork.Vouchers.Update(currentVoucher);
                }
            }

            // --- UPDATE ORDER ---
            order.DiscountAmount = totalDiscountAmount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscountAmount);
            order.VoucherId = null; // Clear old FK - stacking uses OrderVouchers table

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            // --- BUILD RESULT ---
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

            for (int i = 0; i < remainingOrderVouchers.Count; i++)
            {
                var ov = remainingOrderVouchers[i];
                var underlyingVoucher = await _unitOfWork.Vouchers.GetByIdAsync(ov.VoucherId);

                if (underlyingVoucher != null)
                {
                    decimal stepDiscount = CalculateVoucherDiscount(underlyingVoucher, order.SubTotal);

                    if (stepDiscount > (order.SubTotal - totalDiscountAmount))
                    {
                        stepDiscount = order.SubTotal - totalDiscountAmount;
                    }

                    // Update remaining record with new accurate discount and order
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

        private static decimal CalculateVoucherDiscount(Voucher voucher, decimal subTotal)
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
