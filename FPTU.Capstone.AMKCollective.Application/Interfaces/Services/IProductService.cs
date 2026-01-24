using FPTU.Capstone.AMKCollective.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IProductService
    {
        Task<(IEnumerable<PartDto> Items, int TotalCount)> GetListAsync(PartQueryParams query);
        Task<PartDto> GetBySlugAsync(string slug);
        Task<PartDto> CreateAsync(Guid userId, CreateUpdatePartDto request);
        Task UpdateAsync(Guid id, CreateUpdatePartDto request);
        Task DeleteAsync(Guid id);

        Task<IEnumerable<PartDto>> GetRecommendationsAsync(Guid baseKitId, string partType);
        Task<Dictionary<Guid, int>> CheckStockAvailabilityAsync(List<Guid> productIds);
    }
}
