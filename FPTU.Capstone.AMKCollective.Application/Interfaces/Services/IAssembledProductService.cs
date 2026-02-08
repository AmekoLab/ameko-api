using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IAssembledProductService
    {
        Task<PaginatedResult<AssembledProductResponse>> GetAllAsync(int pageNumber, int pageSize);
        Task<IEnumerable<AssembledProductResponse>> GetByShopIdAsync(Guid shopId);
        Task<AssembledProductDetailResponse?> GetByIdAsync(Guid id);
        Task<(Guid Id, string? ErrorMessage)> CreateAsync(CreateAssembledProductRequest request);
        Task<bool> UpdateAsync(Guid id, UpdateAssembledProductRequest request);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> RestoreAsync(Guid id);
    }
}
