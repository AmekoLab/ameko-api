using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
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

        public async Task<(IEnumerable<AssembledProduct> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            var query = _context.AssembledProducts
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
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

        public async Task<IEnumerable<AssembledProduct>> GetByShopIdAsync(Guid shopId)
        {
            //Tuan Note: Join with ProductAssembledDetail and Model to filter by ShopId
            return await _context.AssembledProducts
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                        .ThenInclude(m => m.Shop)
                .Where(ap => !ap.IsDeleted && ap.ProductAssembledDetails.Any(pad => pad.BaseKit.ShopId == shopId))
                .OrderByDescending(ap => ap.CreatedAt)
                .ToListAsync();
        }

        public async Task<AssembledProduct?> GetByIdWithDetailsAsync(Guid id)
        {
            return await _context.AssembledProducts
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.BaseKit)
                .Include(ap => ap.ProductAssembledDetails)
                    .ThenInclude(pad => pad.Component)
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
    }
}
