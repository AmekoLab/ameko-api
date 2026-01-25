using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ShopService :IShopService
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

        public async Task<ShopDto> GetShopPublicProfileAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            
            if (shop == null || shop.Status != ShopStatus.Active || !shop.IsActive)
            {
                throw new KeyNotFoundException("Shop not found or inactive");
            }

            return _mapper.Map<ShopDto>(shop);
        }

        public async Task<(IEnumerable<ShopDto> Items, int TotalCount)> GetMarketplaceShopAsync(string? searchTerm, int page, int size)
        {
            var (items, total) = await _unitOfWork.Shops.GetActiveShopsForUserAsync(searchTerm, page, size);
            return (_mapper.Map<IEnumerable<ShopDto>>(items), total);
        }

        public async Task<ShopDetailDto> GetMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                throw new KeyNotFoundException("You do not have a shop yet.");
            }
            return _mapper.Map<ShopDetailDto>(shop);
        }

        public async Task<ShopDto> RegisterShopAsync(Guid userId, CreateShopRequest request)
        {
            var existingShop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (existingShop != null)
            {
                throw new InvalidOperationException("User already has a shop.");
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
                Status = ShopStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Shops.CreateAsync(shop);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<ShopDto>(shop);
        }

        public async Task UpdateMyShopAsync(Guid userId, UpdateShopRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // [FIX 1] Validate trùng tên Shop khi update
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
            
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        public async Task<(IEnumerable<ShopDetailDto> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size)
        {
            var (items, total) = await _unitOfWork.Shops.GetShopsAsync(searchTerm, status, page, size);
            return (_mapper.Map<IEnumerable<ShopDetailDto>>(items), total);
        }

        public async Task ApproveShopAsync(Guid shopId, ApproveShopRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // [FIX 2] Logic nâng cấp Role User lên Seller
            // Chỉ thực hiện khi trạng thái mới là Active và trạng thái cũ chưa phải Active
            if (request.Status == ShopStatus.Active && shop.Status != ShopStatus.Active)
            {
                var upgradeResult = await _userService.UpgradeToShopAsync(shop.UserId);
                
                if (!upgradeResult.Success)
                {
                    // Nếu không nâng cấp được user (ví dụ chưa confirm email), không cho phép duyệt shop
                    throw new InvalidOperationException($"Cannot approve shop. User upgrade failed: {upgradeResult.ErrorMessage}");
                }
            }

            shop.Status = request.Status;
            shop.AdminNote = request.AdminNote;

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }
    }
}