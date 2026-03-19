using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IAssembledProductRepository
    {
        Task<(IEnumerable<AssembledProduct> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<AssembledProduct>> GetByShopIdAsync(Guid shopId);
        Task<AssembledProduct?> GetByIdWithDetailsAsync(Guid id);
        Task<IEnumerable<AssembledProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task AddAsync(AssembledProduct assembledProduct);
        Task UpdateAsync(AssembledProduct assembledProduct);
        Task DeleteAsync(AssembledProduct assembledProduct);
    }
}
