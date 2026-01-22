using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IShopRepository
    {
        Task<ShopProfile?> GetByIdAsync(Guid id, CancellationToken token = default);

        Task<ShopProfile?> GetByUserIdAsync(Guid userId, CancellationToken token = default);

        Task<bool> IsShopNameExistsAsync(string shopName, CancellationToken token = default);
        Task<bool> IsCitizenIdExistsAsync(string citizenId, CancellationToken token = default);
        Task<bool> IsTaxCodeExistsAsync(string taxCode, CancellationToken token = default);
        Task<bool> IsShopOwnerAsync(Guid shopId, Guid userId, CancellationToken token = default); //check item

        // --- READ (LIST / PAGINATION) ---

        Task<(IEnumerable<ShopProfile> Items, int TotalCount)> GetShopsAsync(
            string? searchTerm,
            ShopStatus? status,
            int pageNumber,
            int pageSize,
            CancellationToken token = default);

        Task<(IEnumerable<ShopProfile> Items, int TotalCount)> GetActiveShopsForUserAsync(
            string? searchTerm,
            int pageNumber,
            int pageSize,
            CancellationToken token = default);

        // --- WRITE ---
        Task CreateAsync(ShopProfile shop, CancellationToken token = default);

        Task UpdateAsync(ShopProfile shop, CancellationToken token = default);
        Task UpdateShopMetricsAsync(Guid shopId, int quantitySold, decimal revenueAmount, CancellationToken token = default);

        // --- TRANSACTION ---
        Task<int> SaveChangesAsync(CancellationToken token = default);
    }
}
