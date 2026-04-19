using FPTU.Capstone.AMKCollective.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ICategoryService
    {
        Task<IEnumerable<CategorySummaryResponse>> GetCategoriesAsync(GetCategoriesFilterRequest queryParams, Guid? userId, CancellationToken cancellationToken = default);
        Task<CategoryResponse?> GetCategoryByIdAsync(Guid id, bool includeSubCategories = false, CancellationToken cancellationToken = default);
        Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, Guid userId, CancellationToken cancellationToken = default);
        Task<CategoryResponse> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteCategoryAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task<CategoryResponse?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken = default);

        // parts
        Task<(IEnumerable<PartSummaryResponse> Items, int TotalCount, int TotalPages)> GetPartsInCategoryAsync(GetPartsFilterRequest queryParams, CancellationToken cancellationToken = default);

        // Utility
        Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<CategorySummaryResponse>> GetRootCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    }
}
