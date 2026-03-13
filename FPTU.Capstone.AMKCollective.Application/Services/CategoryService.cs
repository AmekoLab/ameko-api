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
                false,
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

            // Lấy tên Shop để làm Namespace cho Slug (nếu là Shop tạo)
            string? shopName = null;
            // Coi Guid.Empty là Admin -> không cần lấy tên
            if (shopId.HasValue && shopId != Guid.Empty)
            {
                var shop = await _unitOfWork.Shops.GetByIdAsync(shopId.Value);
                if (shop != null) shopName = shop.ShopName;
            }

            //truyền shopName vào
            string uniqueSlug = await GenerateUniqueSlugAsync(request.Name, shopName, null, cancellationToken);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Slug = uniqueSlug,
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

        public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, Guid? requesterShopId, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id, false, cancellationToken);
            if (category == null) throw new KeyNotFoundException($"Category with id {id} not found.");

            // [Fix #2] Admin luôn có requesterShopId = null (GetCurrentUserContextAsync chỉ set shopId khi role == "Shop")
            // Guid.Empty không bao giờ xảy ra — bỏ condition thừa để tránh hiểu nhầm
            bool isAdmin = !requesterShopId.HasValue;

            // Nếu không phải Admin VÀ ID người gọi không khớp ID chủ sở hữu category -> Chặn
            if (!isAdmin && category.ShopId != requesterShopId)
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
                // Logic Slug khi Update
                // Nếu đổi tên -> Phải tạo lại slug. Cần lấy lại ShopName của category đó (để giữ namespace cũ)
                string? shopName = null;
                if (category.ShopId != null && category.ShopId != Guid.Empty)
                {
                    var shop = await _unitOfWork.Shops.GetByIdAsync(category.ShopId.Value);
                    shopName = shop?.ShopName;
                }

                // Truyền id vào tham số thứ 3 (excludeId) để tránh báo trùng với chính nó
                category.Slug = await GenerateUniqueSlugAsync(request.Name, shopName, id, cancellationToken);
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

        public async Task<bool> DeleteCategoryAsync(Guid id, Guid? requesterShopId, CancellationToken cancellationToken = default)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id, false, cancellationToken);
            if (category == null) return false;

            // [Fix #2] Admin luôn có requesterShopId = null
            bool isAdmin = !requesterShopId.HasValue;

            if (!isAdmin && category.ShopId != requesterShopId)
            {
                throw new UnauthorizedAccessException("You cannot delete a category you do not own.");
            }

            var hasSubCategories = await _unitOfWork.Categories.HasSubCategoriesAsync(id, cancellationToken);
            if (hasSubCategories) throw new InvalidOperationException("Cannot delete category with subcategories.");

            var hasParts = await _unitOfWork.Categories.HasPartsAsync(id, false, cancellationToken);
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
                false,
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

        // [Fix #3] GenerateSlug xử lý Unicode tiếng Việt đầy đủ bằng Normalize NFD + strip diacritics
        private string GenerateSlug(string name)
        {
            var normalized = name.Replace("&", "and").Normalize(System.Text.NormalizationForm.FormD);
            var asciiChars = normalized
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray();
            var slug = new string(asciiChars).ToLower().Trim().Replace(" ", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-").Trim('-');
            return slug;
        }

        private async Task<string> GenerateUniqueSlugAsync(string name, string? shopName, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            // Tạo slug gốc từ tên
            string baseSlug = GenerateSlug(name);

            // Nếu có tên Shop (tức là Shop tạo), ghép thêm vào slug
            if (!string.IsNullOrEmpty(shopName))
            {
                baseSlug = $"{baseSlug}-{GenerateSlug(shopName)}";
            }

            string finalSlug = baseSlug;
            int counter = 1;

            // Vòng lặp kiểm tra trùng
            while (true)
            {
                // Gọi Repository check trùng toàn hệ thống
                var isDuplicate = await _unitOfWork.Categories.IsSlugDuplicateAsync(finalSlug, excludeId, false, cancellationToken);

                if (!isDuplicate)
                {
                    break;
                }

                // Nếu trùng -> Thêm số đếm vào sau
                finalSlug = $"{baseSlug}-{counter}";
                counter++;
            }

            return finalSlug;
        }
    }
}