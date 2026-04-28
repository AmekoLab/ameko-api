using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class AssembledProductRepository : IAssembledProductRepository
    {
        private readonly ApplicationDbContext _context;

        public AssembledProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<AssembledProduct> Items, int TotalCount)> SearchPagedAsync(SearchAssembledProductRequest request, CancellationToken ct = default)
        {
            var query = _context.AssembledProducts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                        .ThenInclude(m => m.Shop)
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.Component)
                        .ThenInclude(m => m.Shop)
                .Where(ap => !ap.IsDeleted);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                query = query.Where(ap => ap.Name.ToLower().Contains(request.SearchTerm.ToLower()));

            if (request.MinPrice.HasValue)
                query = query.Where(ap => ap.Price >= request.MinPrice.Value);

            if (request.MaxPrice.HasValue)
                query = query.Where(ap => ap.Price <= request.MaxPrice.Value);

            if (!string.IsNullOrWhiteSpace(request.Layout))
                query = query.Where(ap => ap.Layout != null && ap.Layout.ToLower().Contains(request.Layout.ToLower()));

            if (!string.IsNullOrWhiteSpace(request.Mounting))
                query = query.Where(ap => ap.Mounting != null && ap.Mounting.ToLower().Contains(request.Mounting.ToLower()));

            if (!string.IsNullOrWhiteSpace(request.PCB))
                query = query.Where(ap => ap.PCB != null && ap.PCB.ToLower().Contains(request.PCB.ToLower()));

            if (!string.IsNullOrWhiteSpace(request.Connection))
                query = query.Where(ap => ap.Connection != null && ap.Connection.ToLower().Contains(request.Connection.ToLower()));

            if (!string.IsNullOrWhiteSpace(request.Battery))
                query = query.Where(ap => ap.Battery != null && ap.Battery.ToLower().Contains(request.Battery.ToLower()));

            if (request.MinRating.HasValue)
                query = query.Where(ap => ap.Rating >= request.MinRating.Value);

            if (request.ShopId.HasValue)
                query = query.Where(ap => ap.CreatedBy == request.ShopId.Value);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(ap => ap.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task<(IEnumerable<AssembledProduct> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            var query = _context.AssembledProducts
                .IgnoreQueryFilters() // Tuan Note: Essential to load shop info even if parts are soft-deleted
                .AsNoTracking()
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                        .ThenInclude(m => m.Shop)
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.Component)
                        .ThenInclude(m => m.Shop)
                .Where(ap => !ap.IsDeleted);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(ap => ap.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<AssembledProduct>> GetByShopIdAsync(Guid userId)
        {
            // Now strictly querying by CreatedBy which stores the UserId. 
            // Avoids issue where soft-deleted components would omit the parent product.
            return await _context.AssembledProducts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                        .ThenInclude(m => m.Shop)
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.Component)
                        .ThenInclude(m => m.Shop)
                .Where(ap => !ap.IsDeleted && ap.CreatedBy == userId)
                .OrderByDescending(ap => ap.CreatedAt)
                .ToListAsync();
        }

        public async Task<AssembledProduct?> GetByIdWithDetailsAsync(Guid id)
        {
            // Note: Do NOT use AsNoTracking here.
            // This method is called by UpdateAsync and DeleteAsync which need EF change tracking.
            // Without tracking, modifications to the entity (Clear/Add details, property changes) 
            // are invisible to EF and CommitAsync saves nothing.
            return await _context.AssembledProducts
                .IgnoreQueryFilters()
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                        .ThenInclude(bk => bk.Shop)
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.Component)
                        .ThenInclude(m => m.Shop)
                .FirstOrDefaultAsync(ap => ap.Id == id);
        }

        public async Task<AssembledProduct?> GetByIdReadOnlyAsync(Guid id)
        {
            // Read-only version for GET endpoints (faster, no tracking overhead)
            return await _context.AssembledProducts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                        .ThenInclude(bk => bk.Shop)
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.Component)
                        .ThenInclude(m => m.Shop)
                .FirstOrDefaultAsync(ap => ap.Id == id);
        }


        public async Task<IEnumerable<AssembledProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            return await _context.AssembledProducts
                .AsNoTracking()
                .Where(ap => ids.Contains(ap.Id) && !ap.IsDeleted)
                .ToListAsync(ct);
        }

        public async Task AddAsync(AssembledProduct assembledProduct)
        {
            await _context.AssembledProducts.AddAsync(assembledProduct);
        }

        public async Task UpdateAsync(AssembledProduct assembledProduct)
        {
            var entry = _context.Entry(assembledProduct);
            //Tuan Note: Only call Update if the entity is detached
            if (entry.State == EntityState.Detached)
            {
                _context.AssembledProducts.Update(assembledProduct);
            }
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(AssembledProduct assembledProduct)
        {
            assembledProduct.IsDeleted = true;
            var entry = _context.Entry(assembledProduct);
            if (entry.State == EntityState.Detached)
            {
                _context.AssembledProducts.Update(assembledProduct);
            }
            await Task.CompletedTask;
        }

        public async Task UpdateEmbeddingAsync(Guid id, string? embedding, CancellationToken ct = default)
        {
            // Scalar update — only writes the Embedding column, does NOT touch ProductAssembledDetails.
            await _context.AssembledProducts
                .Where(ap => ap.Id == id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(ap => ap.Embedding, embedding),
                    ct);
        }
        public async Task<Dictionary<Guid, int>> GetSoldQuantitiesAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
        {
            // Sử dụng DbContext để query thẳng vào OrderItem và nhóm lại tính tổng
            return await _context.Set<OrderItem>()
                .Where(oi => oi.AssembledProductId.HasValue
                          && productIds.Contains(oi.AssembledProductId.Value)
                          && oi.Order.OrderStatus == OrderStatus.Completed) // Chỉ đếm những đơn đã giao thành công / hoàn thành
                .GroupBy(oi => oi.AssembledProductId.Value)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Sold = g.Sum(oi => oi.Quantity)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.Sold, cancellationToken);
        }
    }
}
