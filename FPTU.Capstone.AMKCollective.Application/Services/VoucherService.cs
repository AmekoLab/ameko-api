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
                    discount = voucher.MaxDiscountAmount.Value;
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

            order.VoucherId = null;
            order.DiscountAmount = 0;
            order.TotalAmount = order.SubTotal + order.ShippingFee;

            _unitOfWork.Orders.UpdateOrderAsync(order);
            await _unitOfWork.CommitAsync();
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
