using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ShopService : IShopService
    {
        private readonly IUnitOfWork _unitOfWork; // Chuyển sang dùng UnitOfWork
        private readonly IStorageService _storage;
        private readonly IMapper _mapper;
        private readonly IUserService _userService; // Inject thêm UserService

        public ShopService(
            IUnitOfWork unitOfWork,
            IStorageService storage,
            IMapper mapper,
            IUserService userService)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mapper = mapper;
            _userService = userService;
        }

        public async Task<ShopResponse> GetShopPublicProfileAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);

            if (shop == null || shop.Status != ShopStatus.Active || !shop.IsActive)
            {
                throw new KeyNotFoundException("Shop not found or inactive");
            }

            return _mapper.Map<ShopResponse>(shop);
        }

        public async Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetMarketplaceShopAsync(string? searchTerm, int page, int size)
        {
            var (items, total) = await _unitOfWork.Shops.GetActiveShopsForUserAsync(searchTerm, page, size);
            return (_mapper.Map<IEnumerable<ShopResponse>>(items), total);
        }

        public async Task<ShopDetailResponse> GetMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                throw new KeyNotFoundException("You do not have a shop yet.");
            }
            return _mapper.Map<ShopDetailResponse>(shop);
        }

        public async Task<ShopResponse> RegisterShopAsync(Guid userId, CreateShopRequest request)
        {
            var existingShop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (existingShop != null)
            {
                throw new InvalidOperationException("User already has a shop profile, please update .");
            }

            if (await _unitOfWork.Shops.IsShopNameExistsAsync(request.ShopName))
            {
                throw new ArgumentException("Shop name is already taken.");
            }

            if (await _unitOfWork.Shops.IsCitizenIdExistsAsync(request.CitizenId))
            {
                throw new ArgumentException("Citizen ID is already registered.");
            }

            string logoUrl = null;
            string bannerUrl = null;

            if (request.LogoImage != null)
            {
                logoUrl = await _storage.UploadAsync(
                    request.LogoImage.OpenReadStream(),
                    request.LogoImage.FileName,
                    "shops/logos");
            }

            if (request.BannerImage != null)
            {
                bannerUrl = await _storage.UploadAsync(
                    request.BannerImage.OpenReadStream(),
                    request.BannerImage.FileName,
                    "shops/banners");
            }

            var shop = new ShopProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = request.ShopName,
                Bio = request.Bio,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                ContactEmail = request.ContactEmail,
                CitizenId = request.CitizenId,
                TaxCode = request.TaxCode,
                BankName = request.BankName,
                BankAccountNumber = request.BankAccountNumber,
                BankAccountName = request.BankAccountName,
                LogoUrl = logoUrl,
                BannerUrl = bannerUrl,
                Status = ShopStatus.PendingApproval,
                IsActive = false, // Mặc định là đóng cửa để khi được duyệt shop có thời gian chuẩn bị
                CreatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Shops.CreateAsync(shop);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<ShopResponse>(shop);
        }

        public async Task UpdateMyShopAsync(Guid userId, UpdateShopRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Shop bị ban không được phép cập nhật
            if (shop.Status == ShopStatus.Banned)
                throw new InvalidOperationException("Your shop has been banned. You cannot update your profile.");

            if (!string.IsNullOrEmpty(request.ShopName) && request.ShopName != shop.ShopName)
            {
                if (await _unitOfWork.Shops.IsShopNameExistsAsync(request.ShopName))
                {
                    throw new ArgumentException("Shop name is already taken.");
                }
            }

            if (request.LogoImage != null)
            {
                shop.LogoUrl = await _storage.UploadAsync(
                    request.LogoImage.OpenReadStream(),
                    request.LogoImage.FileName,
                    "shops/logos");
            }

            if (request.BannerImage != null)
            {
                shop.BannerUrl = await _storage.UploadAsync(
                    request.BannerImage.OpenReadStream(),
                    request.BannerImage.FileName,
                    "shops/banners");
            }

            _mapper.Map(request, shop);

            if (shop.Status == ShopStatus.Rejected)
            {
                // --- Resubmit limit: 2 lần mỗi tháng ---
                const int maxResubmitsPerMonth = 2;
                var now = DateTime.UtcNow;

                // Reset counter nếu đã sang tháng mới
                if (shop.LastResubmitTime.HasValue &&
                    (shop.LastResubmitTime.Value.Year != now.Year || shop.LastResubmitTime.Value.Month != now.Month))
                {
                    shop.ResubmitCount = 0;
                }

                if (shop.ResubmitCount >= maxResubmitsPerMonth)
                {
                    throw new InvalidOperationException(
                        $"You have reached the maximum of {maxResubmitsPerMonth} resubmissions this month. Please try again next month.");
                }

                shop.ResubmitCount++;
                shop.LastResubmitTime = now;
                shop.Status = ShopStatus.PendingApproval;
            }

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        public async Task<(IEnumerable<ShopDetailResponse> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size)
        {
            var (items, total) = await _unitOfWork.Shops.GetShopsAsync(searchTerm, status, page, size);
            return (_mapper.Map<IEnumerable<ShopDetailResponse>>(items), total);
        }

        public async Task ApproveShopAsync(Guid shopId, ApproveShopRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Chỉ cho phép Approve/Reject qua endpoint này
            if (request.Status != ShopStatus.Active && request.Status != ShopStatus.Rejected)
                throw new ArgumentException("Only Active or Rejected status is allowed through this endpoint.");

            // Chỉ approve/reject shop đang PendingApproval hoặc Inactive (Admin muốn mở lại)
            if (shop.Status != ShopStatus.PendingApproval && shop.Status != ShopStatus.Inactive)
                throw new InvalidOperationException($"Cannot approve/reject shop with current status: {shop.Status}. Only PendingApproval or Inactive shops can be processed.");

            if (request.Status == ShopStatus.Active)
            {
                var upgradeResult = await _userService.UpgradeToShopAsync(shop.UserId);
                if (!upgradeResult.Success)
                {
                    throw new InvalidOperationException($"Cannot approve shop. User upgrade failed: {upgradeResult.ErrorMessage}");
                }
                shop.Status = ShopStatus.Active;
                shop.AdminNote = request.AdminNote;
                shop.ResubmitCount = 0;
                shop.LastResubmitTime = null;
            }
            else // Rejected
            {
                shop.Status = ShopStatus.Rejected;
                shop.AdminNote = request.AdminNote;
            }

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        /// <summary>
        /// Admin: Deactivate shop — thay đổi Status sang Inactive + force IsActive = false
        /// </summary>
        public async Task AdminDeactivateShopAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            if (shop.Status != ShopStatus.Active)
                throw new InvalidOperationException("Only active shops can be deactivated by admin.");

            shop.Status = ShopStatus.Inactive;  // Admin quyết định Status
            shop.IsActive = false;               // Force inactive
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        /// <summary>
        /// Shop Owner: Tạm nghỉ bán — chỉ thay đổi IsActive, KHÔNG thay đổi Status
        /// </summary>
        public async Task DeactivateMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            if (shop.Status != ShopStatus.Active)
                throw new InvalidOperationException("Only active shops can be deactivated.");

            if (!shop.IsActive)
                throw new InvalidOperationException("Shop is already deactivated.");

            shop.IsActive = false;  // Chủ shop quyết định IsActive
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        /// <summary>
        /// Shop Owner: Mở lại shop — chỉ thay đổi IsActive, KHÔNG thay đổi Status
        /// </summary>
        public async Task ReactivateMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            if (shop.Status != ShopStatus.Active)
                throw new InvalidOperationException("Cannot reactivate. Shop status must be Active (approved by admin).");

            if (shop.IsActive)
                throw new InvalidOperationException("Shop is already active.");

            shop.IsActive = true;  // Chủ shop quyết định IsActive
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        public async Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetAllPendingApprovalShopAsync(int page, int size)
        {
            var (shops, total) = await _unitOfWork.Shops.GetAllPendingApprovalShopAsync(page, size);
            return (_mapper.Map<IEnumerable<ShopResponse>>(shops), total);
        }

        public async Task BannedShopAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");
            shop.Status = ShopStatus.Banned;
            shop.IsActive = false;
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        public async Task UnbanShopAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Check cho chắc chắn
            if (shop.Status != ShopStatus.Banned)
            {
                throw new InvalidOperationException("Shop is not currently banned.");
            }

            // Chuyển về PendingApproval để yêu cầu duyệt lại
            shop.Status = ShopStatus.PendingApproval;

            // Giữ trạng thái ĐÓNG CỬA (IsActive = false)
            shop.IsActive = false;

            // (Tùy chọn) Xóa ghi chú vi phạm cũ hoặc ghi log
             shop.AdminNote = $"Unbanned at {DateTime.UtcNow}. Shop needs approval again.";

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }
    }
}