using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
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

        public async Task<(IEnumerable<PartDto> Items, int TotalCount)> GetListAsync(PartQueryParams query)
        {
            var (entities, total) = await _unitOfWork.Models.GetPagedAsync(query);
            var dtos = _mapper.Map<IEnumerable<PartDto>>(entities);
            return (dtos, total);
        }

        public async Task<PartDto> GetBySlugAsync(string slug)
        {
            var entity = await _unitOfWork.Models.GetBySlugAsync(slug);
            if (entity == null) throw new KeyNotFoundException($"Product with slug '{slug}' not found.");
            return _mapper.Map<PartDto>(entity);
        }

        public async Task<PartDto> CreateAsync(Guid userId, CreateUpdatePartRequest request)
        {
            var entity = _mapper.Map<Model>(request);

            entity.ShopId = await GetShopIdFromUserId(userId);

            entity.Slug = GenerateSlug(entity.Name);
            entity.IsActive = true;
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
            return _mapper.Map<PartDto>(entity);
        }

        public async Task UpdateAsync(Guid id, CreateUpdatePartRequest request)
        {
            var entity = await _unitOfWork.Models.GetByIdAsync(id);
            if (entity == null) throw new KeyNotFoundException("Product not found");
            _mapper.Map(request, entity);
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

        public async Task<IEnumerable<PartDto>> GetRecommendationsAsync(Guid baseKitId, string partType)
        {
            var entities = await _unitOfWork.Models.GetCompatiblePartsAsync(baseKitId, partType);
            return _mapper.Map<IEnumerable<PartDto>>(entities);
        }

        public async Task<Dictionary<Guid, int>> CheckStockAvailabilityAsync(List<Guid> productIds)
        {
            return await _unitOfWork.Models.CheckStockBatchAsync(productIds);
        }

        
    }
}

