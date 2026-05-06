using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storage, IAIService aiService, IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storage = storage;
            _aiService = aiService;
            _cache = cache;
            _scopeFactory = scopeFactory;
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
            // Resolve shopId first (fast) so we can build the dedup cache key before the slow image uploads
            var shopId = await GetShopIdFromUserId(userId);

            // Dedup: same shop + same name within 10 min → trả kết quả cũ, không tạo duplicate.
            // Xử lý trường hợp mạng yếu: response không về client dù server đã commit thành công.
            var dedupKey = $"create-part:{shopId}:{request.Name?.Trim().ToLowerInvariant()}";
            if (_cache.TryGetValue(dedupKey, out PartResponse? cached) && cached != null)
                return cached;

            // 1. Buffer ảnh vào memory TRƯỚC — IFormFile stream chỉ hợp lệ trong request scope.
            // Phải đọc hết vào byte[] trước khi fire background task.
            byte[]? thumbBytes = null;
            string? thumbFileName = null;
            byte[]? layerBytes = null;
            string? layerFileName = null;

            if (request.ThumbnailImage != null)
            {
                using var ms = new MemoryStream();
                await request.ThumbnailImage.OpenReadStream().CopyToAsync(ms);
                thumbBytes = ms.ToArray();
                thumbFileName = request.ThumbnailImage.FileName;
            }

            if (request.LayerImage != null)
            {
                using var ms = new MemoryStream();
                await request.LayerImage.OpenReadStream().CopyToAsync(ms);
                layerBytes = ms.ToArray();
                layerFileName = request.LayerImage.FileName;
            }

            // 2. Map entity và gán dữ liệu cơ bản
            var entity = _mapper.Map<Model>(request);

            entity.ShopId = shopId;
            entity.Slug = GenerateSlug(entity.Name);
            entity.IsActive = true;

            if (!string.IsNullOrEmpty(request.Specifications))
            {
                entity.Specifications = request.Specifications;
            }

            // Logic Fallback (Chỉ chạy khi FE không gửi JSON Specifications)
            if (string.IsNullOrEmpty(entity.Specifications))
            {
                bool hasSwitch = request.RecipeSwitchCount.HasValue && request.RecipeSwitchCount > 0;
                bool hasStab = request.RecipeStabilizerCount.HasValue && request.RecipeStabilizerCount > 0;

                if (hasSwitch || hasStab)
                {
                    var recipeDict = new Dictionary<string, int>();
                    if (hasSwitch) recipeDict.Add("switch", request.RecipeSwitchCount!.Value);
                    if (hasStab) recipeDict.Add("stabilizer", request.RecipeStabilizerCount!.Value);

                    entity.Specifications = JsonSerializer.Serialize(new { recipe = recipeDict });
                }
            }

            // 3. Lưu entity vào DB NGAY (không có ảnh) — trả response nhanh, tránh timeout khi mạng yếu
            await _unitOfWork.Models.CreateAsync(entity);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<PartResponse>(entity);
            response.Specifications = entity.Specifications;
            _cache.Set(dedupKey, response, TimeSpan.FromMinutes(10));

            // 4. Upload ảnh trong background — không block response
            // IFormFile đã được buffer vào byte[] ở bước 1, an toàn khi dùng sau khi request kết thúc.
            if (thumbBytes != null || layerBytes != null)
            {
                var entityId = entity.Id;
                _ = Task.Run(async () =>
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

                    try
                    {
                        var part = await unitOfWork.Models.GetByIdAsync(entityId);
                        if (part == null) return;

                        if (thumbBytes != null)
                            part.ThumbnailURL = await storage.UploadAsync(new MemoryStream(thumbBytes), thumbFileName!, "products");

                        if (layerBytes != null)
                            part.DefaultLayerImageUrl = await storage.UploadAsync(new MemoryStream(layerBytes), layerFileName!, "layers");

                        await unitOfWork.Models.UpdateAsync(part);
                        await unitOfWork.CommitAsync();
                    }
                    catch { /* upload failure is non-critical; part already exists in DB */ }
                });
            }

            try { await _aiService.SyncPartAsync(entity); } catch { }

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

