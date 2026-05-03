using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class ShopRepository : IShopRepository
    {
        private readonly ApplicationDbContext _context;
        public ShopRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ShopProfile?> GetByIdAsync(Guid id, CancellationToken token = default)
        {
            return await _context.ShopProfiles
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, token);
        }

        public async Task<IEnumerable<ShopProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken token = default)
        {
            return await _context.ShopProfiles
                .Include(s => s.User)
                .Where(s => ids.Contains(s.Id) && !s.IsDeleted)
                .ToListAsync(token);
        }

        public async Task<ShopProfile?> GetByUserIdAsync(Guid userId, CancellationToken token = default)
        {
            return await _context.ShopProfiles
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted, token);
        }
        public async Task<bool> IsShopNameExistsAsync(string shopName, CancellationToken token = default)
        {
            return await _context.ShopProfiles
                .AnyAsync(s => s.ShopName.ToLower() == shopName.ToLower() && !s.IsDeleted, token);
        }

        public async Task<bool> IsCitizenIdExistsAsync(string citizenId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(citizenId)) return false;

            return await _context.ShopProfiles
                .AnyAsync(s => s.CitizenId == citizenId && !s.IsDeleted, token);
        }

        public async Task<bool> IsTaxCodeExistsAsync(string taxCode, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(taxCode)) return false;

            return await _context.ShopProfiles
                .AnyAsync(s => s.TaxCode == taxCode && !s.IsDeleted, token);
        }

        public async Task<(IEnumerable<ShopProfile> Items, int TotalCount)> GetShopsAsync(
            string? searchTerm,
            ShopStatus? status,
            int pageNumber,
            int pageSize,
            CancellationToken token = default)
        {
            var query = _context.ShopProfiles
                .Include(s => s.User)
                .Where(s => !s.IsDeleted)
                .AsQueryable();

            // 1. Filter Status 
            if (status.HasValue)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            // 2. Filter SearchTerm (ShopName or Email)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerTerm = searchTerm.ToLower();
                query = query.Where(s =>
                    s.ShopName.ToLower().Contains(lowerTerm) ||
                    s.User.Email.ToLower().Contains(lowerTerm) ||
                    (s.PhoneNumber != null && s.PhoneNumber.Contains(lowerTerm)));
            }

            var totalCount = await query.CountAsync(token);

            //Paging & Sort
            var items = await query
                .OrderByDescending(s => s.CreatedAt) // New shop first 
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(token);

            return (items, totalCount);
        }

        public async Task CreateAsync(ShopProfile shop, CancellationToken token = default)
        {
            await _context.ShopProfiles.AddAsync(shop, token);
        }

        public Task UpdateAsync(ShopProfile shop, CancellationToken token = default)
        {
            if (_context.Entry(shop).State == EntityState.Detached)
            {
                _context.ShopProfiles.Attach(shop);
            }

            _context.ShopProfiles.Update(shop);
            return Task.CompletedTask;
        }

        public Task<int> CountActiveShopsAsync(CancellationToken token = default)
            => _context.ShopProfiles
                .AsNoTracking()
                .CountAsync(s => !s.IsDeleted && s.Status == ShopStatus.Active, token);

        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }
        public async Task<bool> IsShopOwnerAsync(Guid shopId, Guid userId, CancellationToken token = default)
        {
            return await _context.ShopProfiles
                .AnyAsync(s => s.Id == shopId && s.UserId == userId && !s.IsDeleted, token);
        }

        public async Task UpdateShopMetricsAsync(Guid shopId, int quantitySold, decimal revenueAmount, bool includeDeleted = false, CancellationToken token = default)
        {
            var query = _context.ShopProfiles.AsQueryable();
            if (!includeDeleted)
            {
                query = query.Where(s => !s.IsDeleted);
            }
            var shop = await query.FirstOrDefaultAsync(s => s.Id == shopId, token);

            if (shop != null)
            {
                shop.TotalSales += quantitySold;
                shop.TotalRevenue += revenueAmount;
            }
        }

        public async Task<(IEnumerable<ShopProfile> Items, int TotalCount)> GetAllPendingApprovalShopAsync(int page, int size)
        {
            var query = _context.ShopProfiles
                .Where(s => s.Status == ShopStatus.PendingApproval && !s.IsDeleted);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(s => s.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<ShopProfile> Items, int TotalCount)> GetFilteredShopsAsync(
            ShopFilterRequest filter,
            CancellationToken token = default)
        {
            var query = _context.ShopProfiles
                .AsNoTracking()
                .Where(s => !s.IsDeleted &&
                            s.Status == ShopStatus.Active)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var lower = filter.SearchTerm.ToLower();
                query = query.Where(s => s.ShopName.ToLower().Contains(lower));
            }

            if (filter.MinRating.HasValue)
                query = query.Where(s => s.Rating >= filter.MinRating.Value);

            if (filter.MinReviews.HasValue)
                query = query.Where(s => s.TotalReviews >= filter.MinReviews.Value);

            if (filter.Badge.HasValue)
                query = query.Where(s => s.Badge == filter.Badge.Value);

            var totalCount = await query.CountAsync(token);

            query = filter.SortBy switch
            {
                ShopSortBy.TotalReviews  => query.OrderByDescending(s => s.TotalReviews).ThenByDescending(s => s.Rating),
                ShopSortBy.TotalSales    => query.OrderByDescending(s => s.TotalSales).ThenByDescending(s => s.Rating),
                ShopSortBy.Newest        => query.OrderByDescending(s => s.CreatedAt),
                ShopSortBy.QualityScore  => query.OrderByDescending(s => s.CurrentQualityScore).ThenByDescending(s => s.Rating),
                _                        => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.TotalSales),
            };

            var items = await query
                .Skip((filter.Page - 1) * filter.Size)
                .Take(filter.Size)
                .ToListAsync(token);

            return (items, totalCount);
        }

        public async Task UpdateEmbeddingAsync(Guid id, string? embedding, CancellationToken token = default)
        {
            await _context.ShopProfiles
                .Where(s => s.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Embedding, embedding), token);
        }
    }
}
