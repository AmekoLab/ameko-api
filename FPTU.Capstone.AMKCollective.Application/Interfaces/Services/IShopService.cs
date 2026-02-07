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
        Task UpdateMyShopAsync(Guid userId, UpdateShopRequest request);

        Task<(IEnumerable<ShopDetailResponse> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size);
        Task ApproveShopAsync(Guid userId, ApproveShopRequest request);
        Task DeactivateShopAsync(Guid shopId);
        Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetAllPendingApprovalShopAsync(int page, int size);
        Task BannedShopAsync(Guid shopId);
    }
}
