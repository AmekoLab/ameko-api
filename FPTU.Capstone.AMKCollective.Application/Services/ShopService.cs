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
        private readonly IShopRepository _shopRepo;
        //add userrepository here 
        private readonly IStorageService _storage;
        private readonly IMapper _mapper;

        public ShopService(IShopRepository shopRepo, IStorageService storage, IMapper mapper)
        {
            _shopRepo = shopRepo;
            _storage = storage;
            _mapper = mapper;
        }

        public async Task<ShopDto> GetShopPublicProfileAsync(Guid shopId)
        {
            var shop = await _shopRepo.GetByIdAsync(shopId);
            if (shop == null || shop.Status != ShopStatus.Active || !shop.IsActive)
                throw new KeyNotFoundException("Shop not found or inactive");
            
            return _mapper.Map<ShopDto>(shop);
        }

        public async Task<(IEnumerable<ShopDto> Items, int TotalCount)> GetMarketplaceShopAsync(string? searchTerm, int page, int size)
        {
            var (items, total) = await _shopRepo.GetActiveShopsForUserAsync(searchTerm, page, size);
            return (_mapper.Map<IEnumerable<ShopDto>>(items), total);
        }

        public async Task<ShopDetailDto> GetMyShopAsync(Guid userId)
        {
            var shop = await _shopRepo.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("You do not have a shop yet.");
            return _mapper.Map<ShopDetailDto>(shop);
        }

        public async Task<ShopDto> RegisterShopAsync(Guid userId, CreateShopRequest request)
        {
            var existingShop = await _shopRepo.GetByUserIdAsync(userId);
            if (existingShop != null) throw new InvalidOperationException("User already has a shop.");
            if (await _shopRepo.IsShopNameExistsAsync(request.ShopName)) throw new ArgumentException("Shop name is already taken.");
            if (await _shopRepo.IsCitizenIdExistsAsync(request.CitizenId)) throw new ArgumentException("Cititzen ID is already registerd.");

            string logoUrl = null;
            string bannerUrl = null;

            if (request.LogoStream != null)
            {
                logoUrl = await _storage.UploadAsync(request.LogoStream, request.LogoFileName ?? "logo.jpg", "shop/logos");

            }
            if (request.BannerStream != null)
            {
                bannerUrl = await _storage.UploadAsync(request.BannerStream, request.BannerFileName ?? "banner.jpg", "shops/banners");
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
            await _shopRepo.CreateAsync(shop);
            await _shopRepo.SaveChangesAsync();
            return _mapper.Map<ShopDto>(shop);
            
        }

        public async Task UpdateMyShopAsync(Guid userId, UpdateShopProfileRequest request)
        {
            var shop = await _shopRepo.GetByUserIdAsync(userId);
            if (shop == null)
            {
                throw new KeyNotFoundException("Shop not found");
            }
               

            if(request.LogoStream != null)
            {
                shop.LogoUrl = await _storage.UploadAsync(request.LogoStream, request.LogoFileName ?? "logo_upd.jpg", "shops/logos");

            }
            if(request.BannerStream != null)
            {
                shop.BannerUrl = await _storage.UploadAsync(request.BannerStream, request.BannerFileName ?? "banner_upd.jpg", "shops/banners");
            }

            _mapper.Map(request, shop);
            await _shopRepo.UpdateAsync(shop);
            await _shopRepo.SaveChangesAsync();
            
        }

        public async Task<(IEnumerable<ShopDetailDto> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size)
        {
            var (items, total) = await _shopRepo.GetShopsAsync(searchTerm, status, page, size);
            return (_mapper.Map<IEnumerable<ShopDetailDto>>(items), total);
        }    

        public async Task ApproveShopAsync(Guid shopId, ApproveShopRequest request)
        {
            var shop = await _shopRepo.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            shop.Status = request.Status;
            shop.AdminNote = request.AdminNote;

            if(request.Status == ShopStatus.Active)
            {
                // TODO: IMPLEMENT ROLE CHANGE LOGIC HERE
            }
            await _shopRepo.UpdateAsync(shop);
            await _shopRepo.SaveChangesAsync();

        }
    }
}
