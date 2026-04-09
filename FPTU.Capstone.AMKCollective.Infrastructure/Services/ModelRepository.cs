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
            CancellationToken cancellationToken = default)
        {
            var query = _context.Models.AsNoTracking().Where(x => !x.IsDeleted);

            // Filter logic
            if (queryParams.ShopId.HasValue)
                query = query.Where(x => x.ShopId == queryParams.ShopId.Value);

            if (queryParams.CategoryId != Guid.Empty)
                query = query.Where(x => x.CategoryId == queryParams.CategoryId);

            if (!string.IsNullOrEmpty(queryParams.PartType))
                query = query.Where(x => x.PartType == queryParams.PartType);

            if (queryParams.IsActive.HasValue)
                query = query.Where(x => x.IsActive == queryParams.IsActive.Value);

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
                .AsNoTracking()
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
    }
}
  