using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(Guid id, bool includeSubCategories = false, CancellationToken cancellationToken = default);
        Task<IEnumerable<Category>> GetAllAsync(bool? isActive = null, Guid? parentId = null, bool includeSubCategories = false, Guid? shopId = null, CancellationToken cancellationToken = default);
        Task<(IEnumerable<Category> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, bool? isActive = null, Guid? parentId = null, bool includeSubCategories = false, Guid? shopId = null, CancellationToken cancellationToken = default);
        Task<Category> CreateAsync(Category category, CancellationToken cancellationToken = default);
        Task<Category> UpdateAsync(Category category, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        Task<Category?> GetBySlugAsync(string slug, Guid? shopId = null, CancellationToken cancellationToken = default);


        //=============================//
        Task<bool> HasSubCategoriesAsync(Guid categoryId, CancellationToken cancellationToken = default);
        Task<bool> HasPartsAsync(Guid categoryId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Category>> GetSubCategoriesAsync(Guid parentId, bool includeInactive = false, CancellationToken cancellationToken = default);
        Task<IEnumerable<Category>> GetRootCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        //===============================//
        Task<(IEnumerable<Model> Items, int TotalCount)> GetPartsInCategoryAsync(Guid categoryId, int pageNumber, int pageSize, bool? isActive = null, string? partType = null, Guid? shopId = null, CancellationToken cancellationToken = default);

    }
}
