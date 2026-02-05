using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
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

        public AssembledProductService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
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

        public async Task<Guid> CreateAsync(CreateAssembledProductRequest request)
        {
            var assembledProduct = _mapper.Map<AssembledProduct>(request);
            
            await _unitOfWork.AssembledProducts.AddAsync(assembledProduct);
            await _unitOfWork.CommitAsync();

            return assembledProduct.Id;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateAssembledProductRequest request)
        {
            var assembledProduct = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(id);
            if (assembledProduct == null) return false;

            _mapper.Map(request, assembledProduct);
            await _unitOfWork.AssembledProducts.UpdateAsync(assembledProduct);
            await _unitOfWork.CommitAsync();

            return true;
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
