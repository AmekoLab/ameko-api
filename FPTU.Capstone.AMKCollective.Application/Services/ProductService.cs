using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStorageService _storage;
        private readonly IAIService _aiService;

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storage, IAIService aiService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storage = storage;
            _aiService = aiService;
        }

        public async Task<(IEnumerable<PartResponse> Items, int TotalCount)> GetListAsync(GetPartsFilterRequest query, Guid? userId = null)
        {
            bool includeDeleted = false;
            if (userId.HasValue)
            {
                var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId.Value);
                if (shop != null)
                {
                    if (!query.ShopId.HasValue || query.ShopId.Value == shop.Id)
                    {
                        query.ShopId = shop.Id;
                        includeDeleted = true;
                    }
                }
            }

            if (!includeDeleted)
            {
                query.IsActive = true;
            }

            var (entities, total) = await _unitOfWork.Models.GetPagedAsync(query, includeDeleted);
            var dtos = _mapper.Map<IEnumerable<PartResponse>>(entities);
            return (dtos, total);
        }

        public async Task<PartResponse> GetBySlugAsync(string slug)
        {
            var entity = await _unitOfWork.Models.GetBySlugAsync(slug);
            if (entity == null) throw new KeyNotFoundException($"Product with slug '{slug}' not found.");
            return _mapper.Map<PartResponse>(entity);
        }

        public async Task<PartResponse> CreateAsync(Guid userId, CreateUpdatePartRequest request)
        {
            // 1. Map dữ liệu cơ bản
            var entity = _mapper.Map<Model>(request);

            entity.ShopId = await GetShopIdFromUserId(userId);
            entity.Slug = GenerateSlug(entity.Name);
            entity.IsActive = true;

            // Gán trực tiếp để tránh trường hợp AutoMapper bỏ qua hoặc map đè
            if (!string.IsNullOrEmpty(request.Specifications))
            {
                entity.Specifications = request.Specifications;
            }

            // 2. Logic Fallback (Chỉ chạy khi FE không gửi JSON Specifications)
            if (string.IsNullOrEmpty(entity.Specifications))
            {
                // Kiểm tra logic switch/stabilizer 
                bool hasSwitch = request.RecipeSwitchCount.HasValue && request.RecipeSwitchCount > 0;
                bool hasStab = request.RecipeStabilizerCount.HasValue && request.RecipeStabilizerCount > 0;

                if (hasSwitch || hasStab)
                {
                    var recipeDict = new Dictionary<string, int>();
                    if (hasSwitch) recipeDict.Add("switch", request.RecipeSwitchCount!.Value);
                    if (hasStab) recipeDict.Add("stabilizer", request.RecipeStabilizerCount!.Value);

                    var specData = new { recipe = recipeDict };
                    entity.Specifications = JsonSerializer.Serialize(specData);
                }
            }
            // ----------------------------------------

            // 3. Upload ảnh (Giữ nguyên)
            if (request.ThumbnailImage != null)
            {
                entity.ThumbnailURL = await _storage.UploadAsync(
                    request.ThumbnailImage.OpenReadStream(),
                    request.ThumbnailImage.FileName,
                    "products");
            }

            if (request.LayerImage != null)
            {
                entity.DefaultLayerImageUrl = await _storage.UploadAsync(
                    request.LayerImage.OpenReadStream(),
                    request.LayerImage.FileName,
                    "layers");
            }

            await _unitOfWork.Models.CreateAsync(entity);
            await _unitOfWork.CommitAsync();

            try
            {
                await _aiService.SyncPartAsync(entity);
            }
            catch (Exception ex)
            {
                // Non-blocking sync failure
                // In production, maybe queue this or log it properly
            }

            // [FIX] Map response and manually assign Specifications to ensure it is returned
            var response = _mapper.Map<PartResponse>(entity);
            response.Specifications = entity.Specifications;

            return response;
        }

        public async Task UpdateAsync(Guid userId, Guid id, CreateUpdatePartRequest request)
        {
            var entity = await _unitOfWork.Models.GetByIdAsync(id);
            if (entity == null) throw new KeyNotFoundException("Product not found");

            // [Fix #2] Ownership check — chỉ Shop chủ sở mới được sửa
            var shopId = await GetShopIdFromUserId(userId);
            if (entity.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not have permission to update this product.");

            if (!string.IsNullOrEmpty(request.Name) && request.Name != entity.Name)
            {
                var newSlug = GenerateSlug(request.Name);
                var existingProduct = await _unitOfWork.Models.GetBySlugAsync(newSlug);
                if (existingProduct != null && existingProduct.Id != id)
                {
                    newSlug = $"{newSlug}-{Guid.NewGuid().ToString().Substring(0, 4)}";
                }
                entity.Slug = newSlug;
            }

            // [Fix #4] Lưu slug đã generate trước khi mapper chạy để tránh bị ghi đè
            var preservedSlug = entity.Slug;
            _mapper.Map(request, entity);
            entity.Slug = preservedSlug; // Restore slug sau mapper

            // [FIX] Cập nhật thủ công để đảm bảo Specifications được lưu nếu người dùng có gửi lên
            if (!string.IsNullOrEmpty(request.Specifications))
            {
                entity.Specifications = request.Specifications;
            }
            
            // Logic Fallback: Chỉ tự động tạo Specifications từ số lượng nếu trong DB vẫn trống
            if (string.IsNullOrEmpty(entity.Specifications) && 
                ((request.RecipeSwitchCount.HasValue && request.RecipeSwitchCount > 0) ||
                (request.RecipeStabilizerCount.HasValue && request.RecipeStabilizerCount > 0)))
            {
                var recipeDict = new Dictionary<string, int>();
                if (request.RecipeSwitchCount.HasValue && request.RecipeSwitchCount > 0)
                    recipeDict.Add("switch", request.RecipeSwitchCount.Value);
                if (request.RecipeStabilizerCount.HasValue && request.RecipeStabilizerCount > 0)
                    recipeDict.Add("stabilizer", request.RecipeStabilizerCount.Value);
                entity.Specifications = JsonSerializer.Serialize(new { recipe = recipeDict });
            }

            if (request.ThumbnailImage != null)
            {
                entity.ThumbnailURL = await _storage.UploadAsync(
                    request.ThumbnailImage.OpenReadStream(),
                    request.ThumbnailImage.FileName,
                    "products");
            }
            if (request.LayerImage != null)
            {
                entity.DefaultLayerImageUrl = await _storage.UploadAsync(
                    request.LayerImage.OpenReadStream(),
                    request.LayerImage.FileName,
                    "layers");
            }

            await _unitOfWork.Models.UpdateAsync(entity);
            await _unitOfWork.CommitAsync();

            try
            {
                await _aiService.SyncPartAsync(entity);
            }
            catch (Exception ex)
            {
                // Non-blocking
            }
        }

        public async Task DeleteAsync(Guid userId, Guid id)
        {
            var entity = await _unitOfWork.Models.GetByIdAsync(id);
            if (entity == null) throw new KeyNotFoundException("Product not found");

            // [Fix #2] Ownership check — chỉ Shop chủ sở mới được xóa
            var shopId = await GetShopIdFromUserId(userId);
            if (entity.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not have permission to delete this product.");

            // [Fix #3] Không cho xóa nếu part đang có trong order active (InCart/Pending)
            bool isInActiveOrder = await _unitOfWork.Models.IsPartInActiveOrderAsync(id);
            if (isInActiveOrder)
                throw new InvalidOperationException("Cannot delete this product because it is currently in an active order or cart. Please wait until the order is completed or cancelled.");

            await _unitOfWork.Models.DeleteAsync(id);
            await _unitOfWork.CommitAsync();
        }

        private string GenerateSlug(string name)
        {
            // [Fix #6] Normalize Unicode (tiếng Việt) sang ASCII trước khi tạo slug
            var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
            var asciiChars = normalized
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray();
            var slug = new string(asciiChars)
                .ToLower()
                .Trim()
                .Replace(" ", "-");
            // Xóa ký tự đặc biệt (giữ lại letters, digits, dấu gạch ngang)
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-").Trim('-');
            return $"{slug}-{Guid.NewGuid().ToString()[..6]}";
        }

        private async Task<Guid> GetShopIdFromUserId(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                throw new Exception("User does not have a valid Shop Profile. Please create a shop first.");
            }

            return shop.Id;
        }

        public async Task<IEnumerable<PartResponse>> GetRecommendationsAsync(Guid baseKitId, string partType)
        {
            var entities = await _unitOfWork.Models.GetCompatiblePartsAsync(baseKitId, partType);
            return _mapper.Map<IEnumerable<PartResponse>>(entities);
        }

        public async Task<Dictionary<Guid, int>> CheckStockAvailabilityAsync(List<Guid> productIds)
        {
            return await _unitOfWork.Models.CheckStockBatchAsync(productIds);
        }
        public async Task RestoreAsync(Guid userId, Guid id)
        {
            var entity = await _unitOfWork.Models.GetByIdIncludeDeletedAsync(id);
            if (entity == null) throw new KeyNotFoundException("Product not found");

            if (!entity.IsDeleted) throw new InvalidOperationException("Product is already active, not deleted.");
            var shopId = await GetShopIdFromUserId(userId);
            if (entity.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not have permission to restore this product.");

            await _unitOfWork.Models.RestoreAsync(id);
            await _unitOfWork.CommitAsync();
        }

    }
}

