using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
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
    public class KitDesignOptionRepository : IKitDesignOptionRepository
    {
        private readonly ApplicationDbContext _context;
        public KitDesignOptionRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<KitDesignOption>> GetOptionsByBaseKitAsync(Guid baseKitId, CancellationToken token = default)
        {
            return await _context.KitDesignOptions
                .AsNoTracking()
                .Where(x => x.BaseKitId == baseKitId)
                .Include(x => x.Component) 
                .Where(x => !x.Component.IsDeleted && x.Component.IsActive)
                .OrderBy(x => x.StepOrder)
                .ToListAsync(token);
        }

        public async Task<(IEnumerable<KitDesignOption> Items, int TotalCount)> GetCompatiblePartsPagedAsync(
            GetCompatiblePartsRequest query,
            CancellationToken token = default)
        {
            var dbQuery = _context.KitDesignOptions
                .AsNoTracking()
                .Include(x => x.Component)
                .Where(x => x.BaseKitId == query.BaseKitId)
                .Where(x => !x.Component.IsDeleted && x.Component.IsActive);

            if (!string.IsNullOrEmpty(query.PartType))
                dbQuery = dbQuery.Where(x => x.Component.PartType == query.PartType);

            if (!string.IsNullOrEmpty(query.SearchTerm))
            {
                var term = query.SearchTerm.ToLower();
                dbQuery = dbQuery.Where(x => x.Component.Name.ToLower().Contains(term));
            }

            int totalCount = await dbQuery.CountAsync(token);

            var items = await dbQuery
                .OrderBy(x => x.Component.Name)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(token);

            return (items, totalCount);
        }
        public async Task<bool> CheckCompatibilityAsync(Guid baseKitId, Guid componentId, CancellationToken token = default)
        {
            return await _context.KitDesignOptions
                .AnyAsync(x => x.BaseKitId == baseKitId && x.ComponentId == componentId, token);
        }

        public async Task<IEnumerable<Guid>> GetValidComponentIdsAsync(Guid baseKitId, IEnumerable<Guid> componentIds, CancellationToken token = default)
        {
            return await _context.KitDesignOptions
                .AsNoTracking()
                .Where(x => x.BaseKitId == baseKitId && componentIds.Contains(x.ComponentId))
                .Select(x => x.ComponentId)
                .ToListAsync(token);
        }
        public async Task CreateAsync(KitDesignOption option, CancellationToken token = default)
        {
            // Check trùng bao gồm cả Tags (Nhánh hình ảnh tích lũy)
            // Cùng BaseKit + Component + Tags mới là trùng thật sự
            bool exists = await _context.KitDesignOptions.AnyAsync(
                x => x.BaseKitId == option.BaseKitId 
                     && x.ComponentId == option.ComponentId
                     && x.Tags == option.Tags, token);

            if (!exists)
            {
                await _context.KitDesignOptions.AddAsync(option, token);
                await _context.SaveChangesAsync(token);
            }
        }

        public async Task CreateBatchAsync(IEnumerable<KitDesignOption> options, CancellationToken token = default)
        {
            await _context.KitDesignOptions.AddRangeAsync(options, token);
            await _context.SaveChangesAsync(token);
        }

        public async Task UpdateAsync(KitDesignOption option, CancellationToken token = default)
        {
            _context.KitDesignOptions.Update(option);
            await _context.SaveChangesAsync(token);
        }

        public async Task DeleteAsync(Guid id, CancellationToken token = default)
        {
            await _context.KitDesignOptions
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync(token);
        }

        public async Task DeleteByBaseKitAsync(Guid baseKitId, CancellationToken token = default)
        {
            await _context.KitDesignOptions
                .Where(x => x.BaseKitId == baseKitId)
                .ExecuteDeleteAsync(token);
        }
        public async Task<IEnumerable<KitDesignOption>> GetCompatibleOptionsForStepAsync(Guid baseKitId, string stepName, string? requiredTag)
        {
            var query = _context.KitDesignOptions
                .Include(x => x.Component).ThenInclude(c => c.Category)
                .Where(x => x.BaseKitId == baseKitId && x.StepName == stepName)
                .AsQueryable();

            if (!string.IsNullOrEmpty(requiredTag))
            {
                // Fetch all options for this step, then filter in-memory
                // to do EXACT comma-split matching instead of substring Contains.
                // This prevents "grp-abc12" from matching "grp-abc123".
                var allOptions = await query.ToListAsync();

                return allOptions.Where(x =>
                    string.IsNullOrEmpty(x.Tags) || // Universal part — compatible with everything
                    x.Tags.Split(',', StringSplitOptions.TrimEntries)
                           .Contains(requiredTag, StringComparer.OrdinalIgnoreCase)
                ).ToList();
            }

            return await query.ToListAsync();
        }

        public async Task<KitDesignOption?> GetOptionByComponentIdAsync(Guid baseKitId, Guid componentId)
        {
            return await _context.KitDesignOptions
                .FirstOrDefaultAsync(x => x.BaseKitId == baseKitId && x.ComponentId == componentId);
        }

        public async Task<KitDesignOption?> GetByIdAsync(Guid id)
        {
            return await _context.KitDesignOptions
                .Include(x => x.Component).ThenInclude(c => c.Category)
                .FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
 