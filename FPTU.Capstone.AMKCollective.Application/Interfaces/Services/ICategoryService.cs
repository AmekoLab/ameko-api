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
        Task<IEnumerable<CategoryListDto>> GetCategoriesAsync(CategoryQueryParams queryParams, CancellationToken cancellationToken = default);
        Task<CategoryDto?> GetCategoryByIdAsync(Guid id, bool includeSubCategories = false, CancellationToken cancellationToken = default);
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
        Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken = default);

        // parts
        Task<(IEnumerable<PartInCategoryDto> Items, int TotalCount, int TotalPages)> GetPartsInCategoryAsync(PartQueryParams queryParams, CancellationToken cancellationToken = default);

        // Utility
        Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<CategoryListDto>> GetRootCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    }
}
