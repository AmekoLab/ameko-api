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
            var items = await _unitOfWork.AssembledProducts.GetByShopIdAsync(shopId);
            return _mapper.Map<IEnumerable<AssembledProductResponse>>(items);
        }

        public async Task<AssembledProductDetailResponse?> GetByIdAsync(Guid id)
        {
            var item = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            return _mapper.Map<AssembledProductDetailResponse>(item);
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
                 return (Guid.Empty, "One or more selected components do not exist");
            }

            foreach (var model in models)
            {
                if (model.ShopId != shop.Id)
                {
                    return (Guid.Empty, $"Component '{model.Name}' does not belong to your shop");
                }
            }

            var assembledProduct = _mapper.Map<AssembledProduct>(request);
            
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

            var productShopId = assembledProduct.ProductAssembledDetails.FirstOrDefault()?.BaseKit.ShopId;
            if (productShopId != shop.Id)
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
                    return (false, null, "One or more selected components do not exist");
                }

                foreach (var model in models)
                {
                    if (model.ShopId != shop.Id)
                    {
                        return (false, null, $"Component '{model.Name}' does not belong to your shop");
                    }
                }

                //Tuan Note: Clear existing details and add new ones. Because updating details is complex (add, update, delete), for simplicity we clear and re-add.
                assembledProduct.ProductAssembledDetails.Clear();
                foreach (var detailReq in request.Details)
                {
                    var detail = _mapper.Map<ProductAssembledDetail>(detailReq);
                    //Tuan Note: Ensure new detail is added instead of updating existing one, Remove existing Id and set to empty. Because AutoMapper will map the Id from detailReq (which is default Guid.Empty) to detail.Id, causing EF to think it's an existing entity.
                    detail.Id = Guid.Empty; 
                    detail.SoundUrl = detailReq.SoundUrl;
                    assembledProduct.ProductAssembledDetails.Add(detail);
                }
            }

            await _unitOfWork.CommitAsync();

            try { await _aiService.SyncBuildAsync(assembledProduct); } catch { /* Ignore */ }

         
            var updatedEntity = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            var responseData = _mapper.Map<AssembledProductDetailResponse>(updatedEntity);

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
    }
}
