using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IModelRepository
    {
        Task<(IEnumerable<Model> Items, int TotalCount)> GetPagedAsync(PartQueryParams queryParams, CancellationToken token = default);
        Task<Model?> GetByIdAsync(Guid id, CancellationToken token = default);
        Task<Model?> GetBySlugAsync(string slug, CancellationToken token = default);

        Task<IEnumerable<Model>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken token = default);

        Task<IEnumerable<Model>> GetCompatiblePartsAsync(Guid baseKitId, string partType, CancellationToken token = default);
        Task<Dictionary<Guid, int>> CheckStockBatchAsync(IEnumerable<Guid> ids, CancellationToken token = default);
        Task CreateAsync(Model part, CancellationToken token = default);
        Task UpdateAsync(Model part, CancellationToken token = default);
        Task DeleteAsync(Guid id, CancellationToken token = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken token = default);
    }
}

