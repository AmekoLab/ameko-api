using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IKitDesignOptionRepository
    {
        Task<IEnumerable<KitDesignOption>> GetOptionsByBaseKitAsync(Guid baseKitId, CancellationToken token = default);

        //Search/Lazy Load in Builder
        Task<(IEnumerable<KitDesignOption> Items, int TotalCount)> GetCompatiblePartsPagedAsync(GetCompatiblePartsRequest query, CancellationToken token = default);
        Task<IEnumerable<Guid>> GetValidComponentIdsAsync(Guid baseKitId, IEnumerable<Guid> componentIds, CancellationToken token = default);

        Task CreateAsync(KitDesignOption option, CancellationToken token = default);

        Task CreateBatchAsync(IEnumerable<KitDesignOption> options, CancellationToken token = default);
        Task UpdateAsync(KitDesignOption option, CancellationToken token = default);

        Task DeleteAsync(Guid id, CancellationToken token = default);
        Task DeleteByBaseKitAsync(Guid baseKitId, CancellationToken token = default);

        Task<bool> CheckCompatibilityAsync(Guid baseKitId, Guid componentId, CancellationToken token = default);

        Task<IEnumerable<KitDesignOption>> GetCompatibleOptionsForStepAsync(Guid baseKitId, string stepName, string? requiredTag);

        Task<KitDesignOption?> GetOptionByComponentIdAsync(Guid baseKitId, Guid componentId);
    }
}