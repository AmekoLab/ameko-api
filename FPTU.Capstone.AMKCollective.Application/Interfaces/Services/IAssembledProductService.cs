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
        Task<PaginatedResult<AssembledProductResponse>> SearchAsync(SearchAssembledProductRequest request, CancellationToken ct = default);
        Task<PaginatedResult<AssembledProductResponse>> GetAllAsync(int pageNumber, int pageSize);
        Task<IEnumerable<AssembledProductResponse>> GetByShopIdAsync(Guid shopId);
        Task<IEnumerable<AssembledProductResponse>> GetMyAssembledProductsAsync(Guid userId);
        Task<AssembledProductDetailResponse?> GetByIdAsync(Guid id);
        Task<(Guid Id, string? ErrorMessage)> CreateAsync(Guid userId, CreateAssembledProductRequest request);
        Task<(bool Success, AssembledProductDetailResponse? Data, string? ErrorMessage)> UpdateAsync(Guid id, Guid userId, UpdateAssembledProductRequest request);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> RestoreAsync(Guid id);
    }
}
