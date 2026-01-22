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
    public class CustomBuilderService : ICustomBuilderService
    {
        private readonly IKitDesignOptionRepository _kitRepo;
        private readonly IModelRepository _modelRepo;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        public CustomBuilderService(IKitDesignOptionRepository kitRepo, IModelRepository modelRepo, IMapper mapper, IStorageService storageService)
        {
            _kitRepo = kitRepo;
            _modelRepo = modelRepo;
            _mapper = mapper;
            _storageService = storageService;
        }
        public async Task<BuilderConfigDto> GetBuilderConfigAsync(Guid baseKitId)
        {
            var baseKit = await _modelRepo.GetByIdAsync(baseKitId);
            if (baseKit == null) throw new KeyNotFoundException("Base Kit not found");

            var options = await _kitRepo.GetOptionsByBaseKitAsync(baseKitId);

            var steps = options
                .GroupBy(x => new { x.StepName, x.StepOrder }) 
                .Select(g => new BuilderStepDto
                {
                    StepName = g.Key.StepName,
                    StepOrder = g.Key.StepOrder,
                    PartType = g.First().Component.PartType ?? "UNKNOWN",
                    Options = _mapper.Map<List<CompatiblePartDto>>(g.ToList())
                })
                .OrderBy(s => s.StepOrder)
                .ToList();
            return new BuilderConfigDto
            {
                BaseKitId = baseKit.Id,
                BaseKitName = baseKit.Name,
                BaseThumbnail = baseKit.ThumbnailURL ?? "",
                Steps = steps
            };
        }
        public async Task<(IEnumerable<CompatiblePartDto> Items, int TotalCount)> SearchPartsInBuilderAsync(CompatiblePartsQuery query)
        {
            var (entities, total) = await _kitRepo.GetCompatiblePartsPagedAsync(query);
            var dtos = _mapper.Map<IEnumerable<CompatiblePartDto>>(entities);

            return (dtos, total);
        }
        public async Task<bool> ValidateConfigurationAsync(Guid baseKitId, List<Guid> componentIds)
        {
            if (componentIds == null || !componentIds.Any()) return false;
            var validIds = await _kitRepo.GetValidComponentIdsAsync(baseKitId, componentIds);
            return validIds.Count() == componentIds.Count;
        }
        public async Task CreateOptionAsync(CreateKitOptionDto request)
        {
            var entity = _mapper.Map<KitDesignOption>(request);

            if (request.FileStream != null && request.FileStream.Length > 0)
            {
                var fileUrl = await _storageService.UploadAsync(
                    request.FileStream,
                    request.FileName ?? $"layer_{Guid.NewGuid()}.png", 
                    "builder-layers"
                );

                entity.LayerImageUrl = fileUrl;
            }
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();

            await _kitRepo.CreateAsync(entity);
        }

        public async Task DeleteOptionAsync(Guid id)
        {
            await _kitRepo.DeleteAsync(id);
        }

        public async Task BulkCreateOptionsAsync(List<CreateKitOptionDto> requests)
        {
            var entitiesToInsert = new List<KitDesignOption>();
            foreach (var req in requests)
            {
                var entity = _mapper.Map<KitDesignOption>(req);
                if (req.FileStream != null && req.FileStream.Length > 0)
                {
                    var fileUrl = await _storageService.UploadAsync(
                        req.FileStream,
                        req.FileName ?? $"bulk_layer_{Guid.NewGuid()}.png",
                        "builder-layers"
                    );
                    entity.LayerImageUrl = fileUrl;
                }
                if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
                entitiesToInsert.Add(entity);
            }

            if (entitiesToInsert.Any())
            {
                await _kitRepo.CreateBatchAsync(entitiesToInsert);
            }
        }

        public async Task ResetBuilderConfigAsync(Guid baseKitId)
        {
            bool exists = await _modelRepo.ExistsAsync(baseKitId);
            if (!exists) throw new KeyNotFoundException("Base Kit not found");

            await _kitRepo.DeleteByBaseKitAsync(baseKitId);
        }
        public async Task<bool> IsMatchAsync(Guid baseKitId, Guid componentId)
        {
            return await _kitRepo.CheckCompatibilityAsync(baseKitId, componentId);
        }
    }
}
 