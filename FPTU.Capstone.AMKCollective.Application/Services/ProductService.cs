using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
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

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storage)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storage = storage;
        }

        public async Task<(IEnumerable<PartResponse> Items, int TotalCount)> GetListAsync(GetPartsFilterRequest query)
        {
            var (entities, total) = await _unitOfWork.Models.GetPagedAsync(query);
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

            // [FIX] Map response and manually assign Specifications to ensure it is returned
            var response = _mapper.Map<PartResponse>(entity);
            response.Specifications = entity.Specifications;
            
            // [DEBUG] Append debug info if spec is missing/present (Optional, remove later)
            if (string.IsNullOrEmpty(response.Specifications))
            {
                // This means entity.Specifications was null/empty even after logic
                // Implies request.Specifications was null/empty AND fallback logic didn't run or produced emptiness
                // response.Description += " [DEBUG: Spec is NULL]";
            }

            return response;
        }

        public async Task UpdateAsync(Guid id, CreateUpdatePartRequest request)
        {
            var entity = await _unitOfWork.Models.GetByIdAsync(id);
            if (entity == null) throw new KeyNotFoundException("Product not found");
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

            _mapper.Map(request, entity);

            // [FIX] Cập nhật thủ công để đảm bảo Specifications được lưu nếu người dùng có gửi lên
            if (!string.IsNullOrEmpty(request.Specifications))
            {
                entity.Specifications = request.Specifications;
            }
            
            // Logic Fallback: Chỉ tự động tạo Specifications từ số lượng nếu trong DB vẫn trống
            // (Nghĩa là người dùng không gửi Specifications lên)
            if (string.IsNullOrEmpty(entity.Specifications) && 
                ((request.RecipeSwitchCount.HasValue && request.RecipeSwitchCount > 0) ||
                (request.RecipeStabilizerCount.HasValue && request.RecipeStabilizerCount > 0)))
            {
                var recipeDict = new Dictionary<string, int>();

                if (request.RecipeSwitchCount.HasValue && request.RecipeSwitchCount > 0)
                {
                    recipeDict.Add("switch", request.RecipeSwitchCount.Value);
                }

                if (request.RecipeStabilizerCount.HasValue && request.RecipeStabilizerCount > 0)
                {
                    recipeDict.Add("stabilizer", request.RecipeStabilizerCount.Value);
                }

                var specData = new
                {
                    recipe = recipeDict
                };

                entity.Specifications = JsonSerializer.Serialize(specData);
            }
            // ---------------------------------------------------------------------
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
        }

        public async Task DeleteAsync(Guid id)
        {
            await _unitOfWork.Models.DeleteAsync(id);
            await _unitOfWork.CommitAsync();
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLower().Trim().Replace(" ", "-");
            return $"{slug}-{Guid.NewGuid().ToString().Substring(0, 6)}";
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

        
    }
}

