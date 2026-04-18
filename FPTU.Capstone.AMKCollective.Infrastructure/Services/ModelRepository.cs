using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Category;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class ModelRepository : IModelRepository
    {
        private readonly ApplicationDbContext _context;
        public ModelRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<(IEnumerable<Model> Items, int TotalCount)> GetPagedAsync(
            GetPartsFilterRequest queryParams,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Models.AsNoTracking();
            if (!includeDeleted)
            {
                // User thường set cứng, không cho FE override
                query = query.Where(x => !x.IsDeleted);
            }
            else if (queryParams.IsDeleted.HasValue)
            {
                // Shop owner truyền filter cụ thể
                query = query.Where(x => x.IsDeleted == queryParams.IsDeleted.Value);
            }

            // Filter logic
            if (queryParams.ShopId.HasValue)
                query = query.Where(x => x.ShopId == queryParams.ShopId.Value);

            if (queryParams.CategoryId != Guid.Empty)
                query = query.Where(x => x.CategoryId == queryParams.CategoryId);

            if (!string.IsNullOrEmpty(queryParams.PartType))
                query = query.Where(x => x.PartType == queryParams.PartType);

            if (queryParams.IsActive.HasValue)
                query = query.Where(x => x.IsActive == queryParams.IsActive.Value);

            // Filter addon-eligible parts (keycap artisan, special switches, etc.)
            if (queryParams.IsAddonEligible.HasValue)
                query = query.Where(x => x.IsAddonEligible == queryParams.IsAddonEligible.Value);

            if (!string.IsNullOrEmpty(queryParams.SearchTerm))
            {
                var term = queryParams.SearchTerm.ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(term));
            }

            // Execute Count & Paging
            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Include(x => x.Shop)
                .Include(x => x.Category)
                .OrderByDescending(x => x.CreatedAt)
                .Skip((queryParams.PageNumber - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<Model?> GetByIdAsync(Guid? id, CancellationToken cancellationToken = default)
        {
            return await _context.Models
                .Include(x => x.Shop)
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        }

        public async Task<Model?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            return await _context.Models
                .AsNoTracking()
                .Include(x => x.Shop)
                .Include(x => x.Category)
                .Include(x => x.AsBaseKitOptions).ThenInclude(o => o.Component)
                .Include(x => x.AsComponentOptions).ThenInclude(o => o.BaseKit)
                .FirstOrDefaultAsync(x => x.Slug == slug && !x.IsDeleted, cancellationToken);
        }

        public async Task<IEnumerable<Model>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            return await _context.Models
                .Include(x => x.Shop)
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Model>> GetCompatiblePartsAsync(Guid baseKitId, string partType, CancellationToken cancellationToken = default)
        {
            return await _context.Models
                .AsNoTracking()
                .Where(p => p.PartType == partType && !p.IsDeleted && p.IsActive)
                .Where(p => p.AsComponentOptions.Any(opt => opt.BaseKitId == baseKitId))
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<Guid, int>> CheckStockBatchAsync(IEnumerable<Guid> ids, bool includeDeleted = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Models
        .AsNoTracking()
        .Where(x => ids.Contains(x.Id));
            if (!includeDeleted)
            {
                query = query.Where(x => !x.IsDeleted);
            }

            return await query.ToDictionaryAsync(x => x.Id, x => x.StockQuantity, cancellationToken);
        }

        public async Task CreateAsync(Model part, CancellationToken cancellationToken = default)
        {
            await _context.Models.AddAsync(part, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Model part, CancellationToken cancellationToken = default)
        {
            _context.Models.Update(part);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateEmbeddingAsync(Guid partId, string? embedding, CancellationToken cancellationToken = default)
        {
            await _context.Models
                .Where(x => x.Id == partId && !x.IsDeleted)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Embedding, embedding),
                    cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _context.Models
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.IsActive, false),
                    cancellationToken);
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Models.AnyAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        }

        /// <summary>[Fix #3] Kiem tra part co dang xuat hien trong order chua hoan thanh khong.</summary>
        public async Task<bool> IsPartInActiveOrderAsync(Guid partId, CancellationToken token = default)
        {
            var closedStatuses = new[]
            {
                Domain.Enums.OrderStatus.Completed,
                Domain.Enums.OrderStatus.Cancelled,
                Domain.Enums.OrderStatus.Refunded,
                Domain.Enums.OrderStatus.Returned
            };

            return await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.ProductId == partId && !oi.IsDeleted)
                .AnyAsync(oi => !closedStatuses.Contains(oi.Order.OrderStatus), token);
        }

        public async Task<bool> UpdateStockAsync(Guid id, int quantityChange, bool includeDeleted = false,CancellationToken cancellationToken = default)
        {
            var query = _context.Models
        .Where(x => x.Id == id && x.StockQuantity + quantityChange >= 0); // Chặn âm kho
            if (!includeDeleted)
            {
                query = query.Where(x => !x.IsDeleted);
            }

            int rowsAffected = await query
                .ExecuteUpdateAsync(s => s.SetProperty(
                    x => x.StockQuantity,
                    x => x.StockQuantity + quantityChange), cancellationToken);

            return rowsAffected > 0;
        }

        public async Task<Guid> GetShopIdByAssembledProductAsync(Guid assembledProductId, CancellationToken token = default)
        {
            // [STRATEGY] Mirror logic của MappingProfile: ưu tiên CreatedBy (UserId) → ShopProfile.Id
            // Assembled product API dùng CreatedBy làm nguồn ShopId khi không có ProductAssembledDetails

            // Bước 1: Lấy CreatedBy của AssembledProduct
            var createdBy = await _context.AssembledProducts
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(ap => ap.Id == assembledProductId)
                .Select(ap => ap.CreatedBy)
                .FirstOrDefaultAsync(token);

            // Bước 2: Nếu có CreatedBy → tìm ShopProfile.Id (UserId = CreatedBy)
            if (createdBy.HasValue && createdBy.Value != Guid.Empty)
            {
                var shopId = await _context.ShopProfiles
                    .AsNoTracking()
                    .Where(sp => sp.UserId == createdBy.Value)
                    .Select(sp => sp.Id)
                    .FirstOrDefaultAsync(token);

                if (shopId != Guid.Empty)
                    return shopId;
            }

            // Bước 3: Fallback — lấy qua ProductAssembledDetails nếu CreatedBy không có
            var detail = await _context.Set<ProductAssembledDetail>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(pad => pad.AssembledProductId == assembledProductId)
                .Select(pad => new { pad.BaseKitId, pad.ComponentId })
                .FirstOrDefaultAsync(token);

            if (detail == null) return Guid.Empty;

            var modelId = detail.BaseKitId != Guid.Empty ? detail.BaseKitId : detail.ComponentId;
            if (modelId == Guid.Empty) return Guid.Empty;

            return await _context.Models
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.Id == modelId)
                .Select(m => m.ShopId)
                .FirstOrDefaultAsync(token);
        }
    }
}