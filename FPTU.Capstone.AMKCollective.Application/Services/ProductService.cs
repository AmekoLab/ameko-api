using AutoMapper;
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
    public class ProductService :IProductService
    {
        private readonly IModelRepository _repo;
        private readonly IMapper _mapper;
        private readonly IStorageService _storage;

        public ProductService(IModelRepository repo, IMapper mapper, IStorageService storage)
        {
            _repo = repo;
            _mapper = mapper;
            _storage = storage;
        }
        public async Task<(IEnumerable<PartDto> Items, int TotalCount)> GetListAsync(PartQueryParams query)
        {
            var (entities, total) = await _repo.GetPagedAsync(query);
            var dtos = _mapper.Map<IEnumerable<PartDto>>(entities);
            return (dtos, total);
        }

        public async Task<PartDto> GetBySlugAsync(string slug)
        {
            var entity = await _repo.GetBySlugAsync(slug);
            if (entity == null) throw new KeyNotFoundException($"Product with slug '{slug}' not found.");
            return _mapper.Map<PartDto>(entity);
        }

        public async Task<PartDto> CreateAsync(Guid userId, CreateUpdatePartDto request)
        {
            var entity = _mapper.Map<Model>(request);

           // entity.ShopId = await GetShopIdFromUserId(userId);
            entity.ShopId = Guid.Parse("24e763a6-f434-11f0-b7bf-0250f80dfa35");

            entity.Slug = GenerateSlug(entity.Name);
            entity.IsActive = true;
            if (request.ImageStream != null)
            {
                entity.ThumbnailURL = await _storage.UploadAsync(
                    request.ImageStream,
                    request.ImageFileName ?? "thumb.jpg",
                    "products");
            }
            if (request.LayerImageStream != null)
            {
                entity.DefaultLayerImageUrl = await _storage.UploadAsync(
                    request.LayerImageStream,
                    request.LayerImageFileName ?? "layer.png",
                    "layers");
            }
            await _repo.CreateAsync(entity);
            return _mapper.Map<PartDto>(entity);
        }

        public async Task UpdateAsync(Guid id, CreateUpdatePartDto request)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) throw new KeyNotFoundException("Product not found");
            _mapper.Map(request, entity);
            if (request.ImageStream != null)
            {
                entity.ThumbnailURL = await _storage.UploadAsync(request.ImageStream, request.ImageFileName!, "products");
            }
            if (request.LayerImageStream != null)
            {
                entity.DefaultLayerImageUrl = await _storage.UploadAsync(request.LayerImageStream, request.LayerImageFileName!, "layers");
            }

            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            await _repo.DeleteAsync(id);
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLower().Trim().Replace(" ", "-");
            return $"{slug}-{Guid.NewGuid().ToString().Substring(0, 6)}";
        }

        private async Task<Guid> GetShopIdFromUserId(Guid userId)
        {
            //TODO:
            // return _shopRepo.GetByUserId(userId).Id;
            return Guid.NewGuid();
        }

        public async Task<IEnumerable<PartDto>> GetRecommendationsAsync(Guid baseKitId, string partType)
        {
            var entities = await _repo.GetCompatiblePartsAsync(baseKitId, partType);
            return _mapper.Map<IEnumerable<PartDto>>(entities);
        }

        public async Task<Dictionary<Guid, int>> CheckStockAvailabilityAsync(List<Guid> productIds)
        {
            return await _repo.CheckStockBatchAsync(productIds);
        }

        
    }
}

