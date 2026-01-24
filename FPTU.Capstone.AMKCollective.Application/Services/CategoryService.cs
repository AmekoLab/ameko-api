using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CategoryService :ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IStorageService _storage;
        public CategoryService(ICategoryRepository categoryRepository, IStorageService storage)
        {
            _categoryRepository = categoryRepository;
            _storage = storage;
        }

        public async Task<IEnumerable<CategoryListDto>> GetCategoriesAsync(CategoryQueryParams queryParams, CancellationToken cancellationToken = default)
        {
            var (categories, totalCount) = await _categoryRepository.GetPagedAsync(
                queryParams.PageNumber,
                queryParams.PageSize,
                queryParams.IsActive,
                queryParams.ParentId,
                queryParams.IncludeSubCategories,
                cancellationToken);

            var categoryDtos = new List<CategoryListDto>();

            foreach (var category in categories)
            {
                var dto = new CategoryListDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    ThumbnailURL = category.ThumbnailURL,
                    ParentId = category.ParentId,
                    IsActive = category.IsActive,
                    SubCategoryCount = category.SubCategories.Count,
                    PartCount = category.Models.Count
                };
                categoryDtos.Add(dto);
            }

            return categoryDtos;
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id, bool includeSubCategories = false, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(id, includeSubCategories, cancellationToken);

            if (category == null)
                return null;

            return MapToCategoryDto(category, includeSubCategories);
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            if (request.ParentId.HasValue)
            {
                var parentExists = await _categoryRepository.ExistsAsync(request.ParentId.Value, cancellationToken);
                if (!parentExists)
                {
                    throw new ArgumentException("Parent category does not exist.");
                }
            }

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Slug = GenerateSlug(request.Name), 
                ParentId = request.ParentId,
                IsActive = request.IsActive,
                ThumbnailURL = null 
            };

            if (request.ImageStream != null)
            {
                category.ThumbnailURL = await _storage.UploadAsync(
                    request.ImageStream,
                    request.ImageFileName ?? $"category_{category.Id}.jpg", 
                    "categories" 
                );
            }
            var createdCategory = await _categoryRepository.CreateAsync(category, cancellationToken);

            return MapToCategoryDto(createdCategory, false);
        }

        public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(id, false, cancellationToken);

            if (category == null)
            {
                throw new KeyNotFoundException($"Category with id {id} not found.");
            }
            if (request.ParentId.HasValue && request.ParentId.Value != category.ParentId)
            {
                if (request.ParentId.Value == id)
                {
                    throw new ArgumentException("Category cannot be its own parent.");
                }

                var parentExists = await _categoryRepository.ExistsAsync(request.ParentId.Value, cancellationToken);
                if (!parentExists)
                {
                    throw new ArgumentException("Parent category does not exist.");
                }
            }
            if (!string.IsNullOrEmpty(request.Name))
            {
                category.Name = request.Name;
                category.Slug = GenerateSlug(request.Name);
            }

            if (request.ParentId.HasValue) category.ParentId = request.ParentId;
            if (request.IsActive.HasValue) category.IsActive = request.IsActive.Value;
            if (request.ImageStream != null)
            {
                // (Optional)
                // _storage.DeleteAsync(category.ThumbnailURL)

                category.ThumbnailURL = await _storage.UploadAsync(
                    request.ImageStream,
                    request.ImageFileName ?? $"category_{category.Id}_updated.jpg",
                    "categories" 
                );
            }

            var updatedCategory = await _categoryRepository.UpdateAsync(category, cancellationToken);

            return MapToCategoryDto(updatedCategory, false);
        }
        public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(id, false, cancellationToken);

            if (category == null)
                return false;

            var hasSubCategories = await _categoryRepository.HasSubCategoriesAsync(id, cancellationToken);
            if (hasSubCategories)
            {
                throw new InvalidOperationException("Cannot delete category with subcategories.");
            }

            var hasParts = await _categoryRepository.HasPartsAsync(id, cancellationToken);
            if (hasParts)
            {
                throw new InvalidOperationException("Cannot delete category with parts.");
            }

            return await _categoryRepository.DeleteAsync(id, cancellationToken);
        }

        public async Task<(IEnumerable<PartInCategoryDto> Items, int TotalCount, int TotalPages)> GetPartsInCategoryAsync(PartQueryParams queryParams, CancellationToken cancellationToken = default)
        {
            var categoryExists = await _categoryRepository.ExistsAsync(queryParams.CategoryId, cancellationToken);
            if (!categoryExists)
            {
                throw new KeyNotFoundException($"Category with id {queryParams.CategoryId} not found.");
            }

            var (parts, totalCount) = await _categoryRepository.GetPartsInCategoryAsync(
                queryParams.CategoryId,
                queryParams.PageNumber,
                queryParams.PageSize,
                queryParams.IsActive,
                queryParams.PartType,
                queryParams.ShopId,
                cancellationToken);

            var partDtos = parts.Select(p => new PartInCategoryDto
            {
                Id = p.Id,
                Name = p.Name,
                ThumbnailURL = p.ThumbnailURL,
                PartType = p.PartType,
                Description = p.Description,
                StockQuantity = p.StockQuantity,
                ShopId = p.ShopId,
                ShopName = p.Shop?.Bio 
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)queryParams.PageSize);

            return (partDtos, totalCount, totalPages);
        }

        public async Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _categoryRepository.ExistsAsync(id, cancellationToken);
        }

        public async Task<IEnumerable<CategoryListDto>> GetRootCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var categories = await _categoryRepository.GetRootCategoriesAsync(includeInactive, cancellationToken);

            return categories.Select(c => new CategoryListDto
            {
                Id = c.Id,
                Name = c.Name,
                ThumbnailURL = c.ThumbnailURL,
                ParentId = c.ParentId,
                IsActive = c.IsActive,
                SubCategoryCount = c.SubCategories.Count,
                PartCount = c.Models.Count
            }).ToList();
        }

        //mapping
        private CategoryDto MapToCategoryDto(Category category, bool includeSubCategories)
        {
            var dto = new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ThumbnailURL = category.ThumbnailURL,
                ParentId = category.ParentId,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };

            if (includeSubCategories && category.SubCategories.Any())
            {
                dto.SubCategories = category.SubCategories
                    .Select(sc => MapToCategoryDto(sc, false))
                    .ToList();
            }

            return dto;
        }
        public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetBySlugAsync(slug, cancellationToken);
            if (category == null) return null;
            return MapToCategoryDto(category, false);
        }

        private string GenerateSlug(string name)
        {
            return name.ToLower()
                .Replace(" ", "-")
                .Replace("&", "and");
        }
    }
}

