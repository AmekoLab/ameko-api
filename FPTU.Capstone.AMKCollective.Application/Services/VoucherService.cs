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
            voucher.CreatorId = userId;

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
                TargetUserId = targetUserId, // Chỉ khách này dùng được

                // Negotiation: không được stack với bất kỳ ai khác ngoài Compensation
                IsStackable = true,
                StackingPolicy = StackingPolicy.WithCompensationOnly
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
                TargetUserId = targetUserId,

                // Compensation: được stack với tất cả loại voucher khác
                IsStackable = true,
                StackingPolicy = StackingPolicy.All
            };

            await _unitOfWork.Vouchers.AddAsync(voucher);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<VoucherResponse>(voucher);
        }

        // 4. Áp dụng Voucher vào Đơn hàng (Stacking — tối đa 2 voucher)
        public async Task<ApplyVoucherResult> ApplyVoucherAsync(Guid userId, Guid orderId, string code)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized to modify this order.");

            var voucher = await _unitOfWork.Vouchers.GetByCodeAsync(code);
            if (voucher == null) throw new Exception("Voucher not found.");

            // --- VALIDATION CƠ BẢN ---
            if (voucher.Status != VoucherStatus.Active) throw new Exception("Voucher is not active.");
            if (DateTime.Now < voucher.StartDate || DateTime.Now > voucher.EndDate) throw new Exception("Voucher is expired or not yet started.");
            if (voucher.UsedCount >= voucher.UsageLimit) throw new Exception("Voucher usage limit reached.");
            if (voucher.TargetUserId != null && voucher.TargetUserId != userId)
                throw new Exception("This voucher is not applicable to you.");

            // --- VALIDATE SCOPE (Shop Owner vs Admin) ---
            await ValidateVoucherScopeAsync(voucher, order);

            // --- STACKING VALIDATION ---
            var appliedList = (await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId)).ToList();

            // Check trùng voucher
            if (appliedList.Any(av => av.VoucherId == voucher.Id))
                throw new Exception("This voucher has already been applied to this order.");

            // Business rule: tối đa 2 voucher
            if (appliedList.Count >= 2)
                throw new Exception("Maximum 2 vouchers can be applied to one order.");

            if (appliedList.Count == 1)
            {
                var existingType = appliedList[0].VoucherType;
                ValidateStackingCombination(existingType, voucher.Type);
            }

            // --- CHECK MIN ORDER VALUE ---
            if (order.SubTotal < voucher.MinOrderValue)
                throw new Exception($"Order value must be at least {voucher.MinOrderValue:N0} VND to use this voucher.");

            // --- TÍNH DISCOUNT ---
            // Discount của từng voucher tính trên SubTotal gốc (không trên remaining)
            // Tổng giảm được cap để TotalAmount không âm
            decimal alreadyDiscounted = appliedList.Sum(av => av.DiscountApplied);
            decimal discount = CalculateVoucherDiscount(voucher, order.SubTotal);

            // Cap: tổng discount không vượt quá SubTotal
            decimal totalDiscount = Math.Min(alreadyDiscounted + discount, order.SubTotal);
            discount = totalDiscount - alreadyDiscounted; // Điều chỉnh nếu cap xảy ra

            // --- TẠO OrderVoucher ---
            var orderVoucher = new OrderVoucher
            {
                OrderId = orderId,
                VoucherId = voucher.Id,
                VoucherCode = voucher.Code,
                VoucherType = voucher.Type,
                DiscountApplied = discount,
                ApplyOrder = appliedList.Count + 1
            };

            await _unitOfWork.OrderVouchers.AddAsync(orderVoucher);

            // --- UPDATE ORDER ---
            order.DiscountAmount = totalDiscount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscount);
            order.VoucherId = null; // Null hóa FK cũ — stacking dùng OrderVouchers table

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            // --- BUILD RESULT ---
            var updatedList = appliedList.Concat(new[] { orderVoucher }).OrderBy(x => x.ApplyOrder).ToList();
            return BuildApplyResult(order, updatedList);
        }

        // 5. Gỡ một voucher cụ thể khỏi đơn hàng
        public async Task<ApplyVoucherResult> RemoveSpecificVoucherAsync(Guid userId, Guid orderId, string voucherCode)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized.");

            var voucher = await _unitOfWork.Vouchers.GetByCodeAsync(voucherCode);
            if (voucher == null) throw new Exception("Voucher not found.");

            var orderVoucher = await _unitOfWork.OrderVouchers.GetByOrderAndVoucherAsync(orderId, voucher.Id);
            if (orderVoucher == null) throw new Exception("This voucher is not applied to the order.");

            _unitOfWork.OrderVouchers.Delete(orderVoucher);

            // Lấy danh sách còn lại sau khi xóa
            var remaining = (await _unitOfWork.OrderVouchers.GetByOrderIdAsync(orderId))
                .Where(ov => ov.Id != orderVoucher.Id)
                .OrderBy(ov => ov.ApplyOrder)
                .ToList();

            // Renumber ApplyOrder
            for (int i = 0; i < remaining.Count; i++) remaining[i].ApplyOrder = i + 1;

            decimal totalDiscount = remaining.Sum(ov => ov.DiscountApplied);
            order.DiscountAmount = totalDiscount;
            order.TotalAmount = Math.Max(0, (order.SubTotal + order.ShippingFee) - totalDiscount);

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();

            return BuildApplyResult(order, remaining);
        }

        // 6. Gỡ toàn bộ voucher khỏi đơn hàng
        public async Task RemoveAllVouchersAsync(Guid userId, Guid orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found.");
            if (order.CustomerId != userId) throw new Exception("Unauthorized.");

            await _unitOfWork.OrderVouchers.DeleteAllByOrderIdAsync(orderId);

            order.VoucherId = null;
            order.DiscountAmount = 0;
            order.TotalAmount = order.SubTotal + order.ShippingFee;

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
        }

        // ─── Private Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra business rule stacking: loại voucher nào được kết hợp với loại nào.
        /// Rules:
        ///   Promotion  + Compensation  ✅
        ///   Negotiation + Compensation ✅
        ///   Promotion  + Promotion     ❌
        ///   Negotiation + Negotiation  ❌
        ///   Promotion  + Negotiation   ❌
        ///   Compensation + Compensation ❌
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
                    break; // Luôn hợp lệ
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

        // 6. Lấy danh sách voucher hợp lệ cho User
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
