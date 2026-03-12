using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IProductService
    {
        Task<(IEnumerable<PartResponse> Items, int TotalCount)> GetListAsync(GetPartsFilterRequest query);
        Task<PartResponse> GetBySlugAsync(string slug);
        Task<PartResponse> CreateAsync(Guid userId, CreateUpdatePartRequest request);
        Task UpdateAsync(Guid userId, Guid id, CreateUpdatePartRequest request);
        Task DeleteAsync(Guid userId, Guid id);

        Task<IEnumerable<PartResponse>> GetRecommendationsAsync(Guid baseKitId, string partType);
        Task<Dictionary<Guid, int>> CheckStockAvailabilityAsync(List<Guid> productIds);
    }
}
