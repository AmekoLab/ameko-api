using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AssembledProductService : IAssembledProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAIService _aiService;

        public AssembledProductService(IUnitOfWork unitOfWork, IMapper mapper, IAIService aiService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _aiService = aiService;
        }

        public async Task<PaginatedResult<AssembledProductResponse>> GetAllAsync(int pageNumber, int pageSize)
        {
            var (items, totalCount) = await _unitOfWork.AssembledProducts.GetAllPagedAsync(pageNumber, pageSize);
            var dtos = _mapper.Map<IEnumerable<AssembledProductResponse>>(items);
            await PopulateShopInfoAsync(dtos);

            return new PaginatedResult<AssembledProductResponse>
            {
                TotalCount = totalCount,
                Items = dtos,
                CurrentPage = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<AssembledProductResponse>> GetByShopIdAsync(Guid shopId)
        {
            // Tuan Note: shopId can be UserId or ShopProfile.Id. 
            // Since our repository now filters by CreatedBy (which is UserId), we must resolve the actual UserId.
            Guid actualUserId = shopId;
            var shopByProfileId = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shopByProfileId != null) 
            {
                actualUserId = shopByProfileId.UserId; // Found a shop by profile ID, extract its UserId
            }

            var items = await _unitOfWork.AssembledProducts.GetByShopIdAsync(actualUserId);
            var dtos = _mapper.Map<IEnumerable<AssembledProductResponse>>(items);
            await PopulateShopInfoAsync(dtos);
            return dtos;
        }
        
        public async Task<IEnumerable<AssembledProductResponse>> GetMyAssembledProductsAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) return new List<AssembledProductResponse>();
            
            // Pass userId directly as GetByShopIdAsync handles it correctly
            return await GetByShopIdAsync(userId);
        }

        public async Task<AssembledProductDetailResponse?> GetByIdAsync(Guid id)
        {
            var item = await _unitOfWork.AssembledProducts.GetByIdReadOnlyAsync(id);
            var dto = _mapper.Map<AssembledProductDetailResponse>(item);
            if (dto != null)
            {
                await PopulateShopInfoAsync(new[] { dto });
            }
            return dto;
        }

        public async Task<(Guid Id, string? ErrorMessage)> CreateAsync(Guid userId, CreateAssembledProductRequest request)
        {
            if (request.Details == null || request.Details.Count == 0)
            {
                return (Guid.Empty, "Assembled product must have at least one detail");
            }

            foreach (var detail in request.Details)
            {
                if (detail.Quantity <= 0)
                {
                    return (Guid.Empty, "Quantity of each detail must be greater than 0");
                }
            }

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                return (Guid.Empty, "User does not have an active shop profile");
            }

            var modelIds = request.Details.Select(d => d.BaseKitId)
                .Concat(request.Details.Select(d => d.ComponentId))
                .Distinct()
                .ToList();

            var models = await _unitOfWork.Models.GetByIdsAsync(modelIds);
            if (models.Count() != modelIds.Count)
            {
                 return (Guid.Empty, "One or more selected components do not exist or have been deleted");
            }

            foreach (var model in models)
            {
                if (model.ShopId != shop.Id)
                {
                    return (Guid.Empty, $"Component '{model.Name}' does not belong to your shop");
                }
                if (!model.IsActive)
                {
                    return (Guid.Empty, $"Component '{model.Name}' is no longer active");
                }
            }

            var assembledProduct = _mapper.Map<AssembledProduct>(request);
            assembledProduct.CreatedBy = userId; // Tuan Note: Track who created this product

            // Fix: AutoMapper does not set AssembledProductId on children (it's not in the DTO).
            // EF Core may not override an already-set Guid.Empty FK value, so we assign it explicitly.
            foreach (var detail in assembledProduct.ProductAssembledDetails)
            {
                detail.AssembledProductId = assembledProduct.Id;
            }

            await _unitOfWork.AssembledProducts.AddAsync(assembledProduct);
            await _unitOfWork.CommitAsync();

            try { await _aiService.SyncBuildAsync(assembledProduct); } catch { /* Ignore */ }

            return (assembledProduct.Id, null);
        }

        public async Task<(bool Success, AssembledProductDetailResponse? Data, string? ErrorMessage)> UpdateAsync(Guid id, Guid userId, UpdateAssembledProductRequest request)
        {
            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            if (assembledProduct == null) return (false, null, "Assembled product not found");

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) return (false, null, "User does not have an active shop profile");     

            // Verify permission using CreatedBy (which is the user's ID)
            if (assembledProduct.CreatedBy != userId)
            {
                return (false, null, "You do not have permission to update this product");
            }

            _mapper.Map(request, assembledProduct);
           
            if (request.Details != null && request.Details.Count > 0)
            {
           
                foreach (var detail in request.Details)
                {
                    if (detail.Quantity <= 0)
                    {
                        return (false, null, "Quantity of each detail must be greater than 0");
                    }
                }

                var modelIds = request.Details.Select(d => d.BaseKitId)
                    .Concat(request.Details.Select(d => d.ComponentId))
                    .Distinct()
                    .ToList();

                var models = await _unitOfWork.Models.GetByIdsAsync(modelIds);
                if (models.Count() != modelIds.Count)
                {
                    return (false, null, "One or more selected components do not exist or have been deleted");
                }

                foreach (var model in models)
                {
                    if (model.ShopId != shop.Id)
                    {
                        return (false, null, $"Component '{model.Name}' does not belong to your shop");
                    }
                    if (!model.IsActive)
                    {
                        return (false, null, $"Component '{model.Name}' is no longer active");
                    }
                }

                // Clear existing details and re-add. This is simpler than tracking individual add/update/delete operations.
                assembledProduct.ProductAssembledDetails.Clear();
                foreach (var detailReq in request.Details)
                {
                    var detail = _mapper.Map<ProductAssembledDetail>(detailReq);
                    // Fix: BaseEntity constructor already generates a new Guid for Id.
                    // Do NOT override with Guid.Empty — that causes EF Core to fail on PK/FK constraint.
                    // Explicitly assign AssembledProductId so the FK is always correct.
                    detail.AssembledProductId = assembledProduct.Id;
                    detail.SoundUrl = detailReq.SoundUrl;
                    assembledProduct.ProductAssembledDetails.Add(detail);
                }
            }

            await _unitOfWork.CommitAsync();

            try { await _aiService.SyncBuildAsync(assembledProduct); } catch { /* Ignore */ }

         
            var updatedEntity = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            var responseData = _mapper.Map<AssembledProductDetailResponse>(updatedEntity);
            if (responseData != null)
            {
                await PopulateShopInfoAsync(new[] { responseData });
            }

            return (true, responseData, null);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            if (assembledProduct == null) return false;

            await _unitOfWork.AssembledProducts.DeleteAsync(assembledProduct);
            await _unitOfWork.CommitAsync();

            return true;
        }

        public async Task<bool> RestoreAsync(Guid id)
        {
            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            if (assembledProduct == null) return false;

            assembledProduct.IsDeleted = false;
            await _unitOfWork.AssembledProducts.UpdateAsync(assembledProduct);
            await _unitOfWork.CommitAsync();

            return true;
        }

        private async Task PopulateShopInfoAsync(IEnumerable<AssembledProductResponse> dtos)
        {
            if (dtos == null) return;
            var shopUserIds = dtos.Where(d => d.ShopId != Guid.Empty).Select(d => d.ShopId).Distinct().ToList();
            var shopDict = new Dictionary<Guid, ShopProfile>();

            foreach (var id in shopUserIds)
            {
                var shop = await _unitOfWork.Shops.GetByUserIdAsync(id);
                if (shop != null)
                {
                    shopDict[id] = shop;
                }
            }

            foreach (var dto in dtos)
            {
                if (shopDict.TryGetValue(dto.ShopId, out var shop))
                {
                    dto.ShopName = shop.ShopName;
                    dto.LogoUrl = shop.LogoUrl;
                }
            }
        }
    }
}
