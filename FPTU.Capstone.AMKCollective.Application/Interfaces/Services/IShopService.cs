using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IShopService
    {
        Task<ShopResponse> GetShopPublicProfileAsync(Guid shopId);
        Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetMarketplaceShopAsync(string? searchTerm, int page, int size);
        Task<ShopDetailResponse> GetMyShopAsync(Guid userId);
        Task<ShopResponse> RegisterShopAsync(Guid userId, CreateShopRequest request);
        Task PatchMyShopAsync(Guid userId, PatchShopRequest request); 
        Task UpdateMyShopRejectedAsync(Guid userId, UpdateShopRejectedRequest request); 

        Task<(IEnumerable<ShopDetailResponse> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size);
        Task ApproveShopAsync(Guid shopId, ApproveShopRequest request);

        // Admin: thay đổi Status (Inactive, Banned)
        Task AdminDeactivateShopAsync(Guid shopId);
        Task BannedShopAsync(Guid shopId);
        Task UnbanShopAsync(Guid shopId);
        // Shop Owner: thay đổi IsActive (không ảnh hưởng Status)
        Task DeactivateMyShopAsync(Guid userId);
        Task ReactivateMyShopAsync(Guid userId);

        Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetAllPendingApprovalShopAsync(int page, int size);

        Task UpdateBankInfoAsync(Guid userId, UpdateBankInfoRequest request);
    }
}
