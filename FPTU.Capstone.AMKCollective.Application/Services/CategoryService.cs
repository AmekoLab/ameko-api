using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storage;
        private readonly IMapper _mapper;

        public CategoryService(IUnitOfWork unitOfWork, IStorageService storage, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CategorySummaryResponse>> GetCategoriesAsync(GetCategoriesFilterRequest queryParams, CancellationToken cancellationToken = default)
        {
            var (categories, totalCount) = await _unitOfWork.Categories.GetPagedAsync(
                queryParams.PageNumber,
                queryParams.PageSize,
                queryParams.IsActive,
                queryParams.ParentId,
                queryParams.IncludeSubCategories,
                queryParams.ShopId, // Pass shopId filter
                cancellationToken);

            // Manual mapping để kiểm soát dữ liệu tốt hơn
            var categoryDtos = categories.Select(c => new CategorySummaryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ThumbnailURL = c.ThumbnailURL,
                ParentId = c.ParentId,
                IsActive = c.IsActive,
                ShopId = c.ShopId,
                SubCategoryCount = c.SubCategories.Count,
                PartCount = c.Models.Count
            }).ToList();

            return categoryDtos;
        }

        public async Task<CategoryResponse?> GetCategoryByIdAsync(Guid id, bool includeSubCategories = false, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id, includeSubCategories, cancellationToken);
            if (category == null) return null;
            return MapToCategoryDto(category, includeSubCategories);
        }

        public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, Guid? shopId, CancellationToken cancellationToken = default)
        {
            if (request.ParentId.HasValue)
            {
                var parentExists = await _unitOfWork.Categories.ExistsAsync(request.ParentId.Value, cancellationToken);
                if (!parentExists) throw new ArgumentException("Parent category does not exist.");
            }

            // [FIX 1] Check trùng Slug khi tạo mới
            var slug = GenerateSlug(request.Name);
            var existingSlug = await _unitOfWork.Categories.GetBySlugAsync(slug, shopId, cancellationToken);
            if (existingSlug != null)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString().Substring(0, 4)}";
            }

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Slug = slug,
                ParentId = request.ParentId,
                IsActive = request.IsActive,
                ShopId = shopId, // Can be null (global) or specific Shop ID (private)
                ThumbnailURL = null
            };

            if (request.ThumbnailImage != null)
            {
                category.ThumbnailURL = await _storage.UploadAsync(
                    request.ThumbnailImage.OpenReadStream(),
                    request.ThumbnailImage.FileName,
                    "categories"
                );
            }

            await _unitOfWork.Categories.CreateAsync(category); 
            await _unitOfWork.CommitAsync();

            return MapToCategoryDto(category, false);
        }

        public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, Guid? shopId, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id, false, cancellationToken);
            if (category == null) throw new KeyNotFoundException($"Category with id {id} not found.");

            // Validate Owner
            if (shopId.HasValue && category.ShopId != shopId)
            {
                throw new UnauthorizedAccessException("You cannot update a category you do not own.");
            }

            if (request.ParentId.HasValue && request.ParentId.Value != category.ParentId)
            {
                if (request.ParentId.Value == id) throw new ArgumentException("Category cannot be its own parent.");
                var parentExists = await _unitOfWork.Categories.ExistsAsync(request.ParentId.Value, cancellationToken);
                if (!parentExists) throw new ArgumentException("Parent category does not exist.");
            }
            if (!string.IsNullOrEmpty(request.Name))
            {
                category.Name = request.Name;
            }

            if (request.ParentId.HasValue) category.ParentId = request.ParentId;
            if (request.IsActive.HasValue) category.IsActive = request.IsActive.Value;

            if (request.ThumbnailImage != null)
            {
                category.ThumbnailURL = await _storage.UploadAsync(
                    request.ThumbnailImage.OpenReadStream(),
                    request.ThumbnailImage.FileName,
                    "categories"
                );
            }

            await _unitOfWork.Categories.UpdateAsync(category); 
            await _unitOfWork.CommitAsync();

            return MapToCategoryDto(category, false);
        }

        public async Task<bool> DeleteCategoryAsync(Guid id, Guid? shopId, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id, false, cancellationToken);
            if (category == null) return false;

            // Validate Owner
            if (shopId.HasValue && category.ShopId != shopId)
            {
                throw new UnauthorizedAccessException("You cannot delete a category you do not own.");
            }

            var hasSubCategories = await _unitOfWork.Categories.HasSubCategoriesAsync(id, cancellationToken);
            if (hasSubCategories) throw new InvalidOperationException("Cannot delete category with subcategories.");

            var hasParts = await _unitOfWork.Categories.HasPartsAsync(id, cancellationToken);
            if (hasParts) throw new InvalidOperationException("Cannot delete category with parts.");

            var result = await _unitOfWork.Categories.DeleteAsync(id, cancellationToken);
            await _unitOfWork.CommitAsync();
            return result;
        }

        public async Task<(IEnumerable<PartSummaryResponse> Items, int TotalCount, int TotalPages)> GetPartsInCategoryAsync(GetPartsFilterRequest queryParams, CancellationToken cancellationToken = default)
        {
            var categoryExists = await _unitOfWork.Categories.ExistsAsync(queryParams.CategoryId, cancellationToken);
            if (!categoryExists) throw new KeyNotFoundException($"Category with id {queryParams.CategoryId} not found.");

            var (parts, totalCount) = await _unitOfWork.Categories.GetPartsInCategoryAsync(
                queryParams.CategoryId,
                queryParams.PageNumber,
                queryParams.PageSize,
                queryParams.IsActive,
                queryParams.PartType,
                queryParams.ShopId,
                cancellationToken);

            var partDtos = parts.Select(p => new PartSummaryResponse
            {
                Id = p.Id,
                Name = p.Name,
                ThumbnailURL = p.ThumbnailURL,
                PartType = p.PartType,
                Description = p.Description,
                StockQuantity = p.StockQuantity,
                ShopId = p.ShopId,
                ShopName = p.Shop?.ShopName 
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)queryParams.PageSize);
            return (partDtos, totalCount, totalPages);
        }

        public async Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.Categories.ExistsAsync(id, cancellationToken);
        }

        public async Task<IEnumerable<CategorySummaryResponse>> GetRootCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var categories = await _unitOfWork.Categories.GetRootCategoriesAsync(includeInactive, cancellationToken);
            return categories.Select(c => new CategorySummaryResponse
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

        public async Task<CategoryResponse?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.Categories.GetBySlugAsync(slug, null, cancellationToken);
            if (category == null) return null;
            return MapToCategoryDto(category, false);
        }

        // Helpers
        private CategoryResponse MapToCategoryDto(Category category, bool includeSubCategories)
        {
            var dto = new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ThumbnailURL = category.ThumbnailURL,
                ParentId = category.ParentId,
                IsActive = category.IsActive,
                ShopId = category.ShopId,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };

            if (includeSubCategories && category.SubCategories != null && category.SubCategories.Any())
            {
                dto.SubCategories = category.SubCategories
                    .Select(sc => MapToCategoryDto(sc, false))
                    .ToList();
            }
            return dto;
        }

        private string GenerateSlug(string name)
        {
            return name.ToLower()
                .Replace(" ", "-")
                .Replace("&", "and")
                .Replace("đ", "d"); // Thêm xử lý tiếng Việt đơn giản nếu cần
        }
    }
}