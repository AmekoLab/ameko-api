using FPTU.Capstone.AMKCollective.Application.DTOs;
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
        Task<ShopDto> GetShopPublicProfileAsync(Guid shopId);
        Task<(IEnumerable<ShopDto> Items, int TotalCount)> GetMarketplaceShopAsync(string? searchTerm, int page, int size);
        Task<ShopDetailDto> GetMyShopAsync(Guid userId);
        Task<ShopDto> RegisterShopAsync(Guid userId, CreateShopRequest request);
        Task UpdateMyShopAsync(Guid userId, UpdateShopProfileRequest request);

        Task<(IEnumerable<ShopDetailDto> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size);
        Task ApproveShopAsync(Guid userId, ApproveShopRequest request);
    }
}
