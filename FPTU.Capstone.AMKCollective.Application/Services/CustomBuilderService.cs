using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
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
    public class CustomBuilderService : ICustomBuilderService
    {
        private readonly IKitDesignOptionRepository _kitRepo;
        private readonly IModelRepository _modelRepo;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly IBuilderSessionRepository _sessionRepo;
        public CustomBuilderService(IKitDesignOptionRepository kitRepo, IModelRepository modelRepo, IMapper mapper, IStorageService storageService, IBuilderSessionRepository sessionRepo)
        {
            _kitRepo = kitRepo;
            _modelRepo = modelRepo;
            _mapper = mapper;
            _storageService = storageService;
            _sessionRepo = sessionRepo;
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

        // ===================================SERVER-DRIVEN BUILDER======================================//


        public async Task<BuilderStepResponse> StartBuilderSessionAsync(BuilderStartRequest request)
        {
            // 1. Validate Base Kit
            var baseKit = await _modelRepo.GetByIdAsync(request.BaseKitId);
            if (baseKit == null) throw new KeyNotFoundException("Base Kit not found");

            // 2. Tạo Session Mới
            var newSession = new BuilderSession
            {
                Id = Guid.NewGuid(),
                BaseKitId = request.BaseKitId,
                CurrentStep = "case", // Mặc định bước đầu tiên là Case
                SelectedItemsJson = "{}",
                TotalPrice = baseKit.Price, // Giá khởi điểm = Giá Kit gốc
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            await _sessionRepo.CreateSessionAsync(newSession);

            // 3. Lấy linh kiện cho bước đầu tiên (Case)
            var firstStepOptions = await _kitRepo.GetCompatibleOptionsForStepAsync(request.BaseKitId, "case", null);

            // 4. Trả về Response
            return ConstructResponse(newSession, "case", 1, _mapper.Map<List<CompatiblePartDto>>(firstStepOptions));
        }

        public async Task<BuilderStepResponse> SelectPartAsync(BuilderSelectRequest request)
        {
            // 1. Lấy Session
            var session = await _sessionRepo.GetSessionByIdAsync(request.SessionId);
            if (session == null) throw new KeyNotFoundException("Session expired or not found");

            // 2. Lấy thông tin linh kiện vừa chọn để check Tags
            // Tìm trong bảng KitDesignOption để lấy cả Tags và Rule
            var selectedOption = (await _kitRepo.GetCompatibleOptionsForStepAsync(session.BaseKitId, request.StepName, null))
                                .FirstOrDefault(x => x.ComponentId == request.SelectedPartId);

            if (selectedOption == null) throw new KeyNotFoundException("Selected part is not valid for this kit");

            // 3. Cập nhật Session (Selection & Price)
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartDetail>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartDetail>();

            // Lưu món vừa chọn vào Dictionary
            currentSelection[request.StepName] = new SelectedPartDetail
            {
                Id = selectedOption.ComponentId,
                Name = selectedOption.Component.Name,
                Price = selectedOption.Component.Price,
                ThumbnailUrl = selectedOption.Component.ThumbnailURL
            };

            // Tính lại tổng tiền (Giá Base + Tổng linh kiện)
            decimal newTotal = session.BaseKit.Price;
            foreach (var item in currentSelection.Values)
            {
                newTotal += item.Price;
            }

            session.SelectedItemsJson = JsonSerializer.Serialize(currentSelection);
            session.TotalPrice = newTotal;

            // 4. Xác định Bước Tiếp Theo (Next Step Logic)
            var (nextStepName, nextStepOrder) = GetNextStepInfo(request.StepName);

            session.CurrentStep = nextStepName;
            await _sessionRepo.UpdateSessionAsync(session);

            // 5. Lấy linh kiện cho bước tiếp theo (Có Lọc)
            List<CompatiblePartDto> nextProducts = new();
            if (nextStepName != "complete")
            {
                // Logic Lọc Quan Trọng:
                // Lấy filter rule từ món vừa chọn (VD: "plate:LAYOUT_65")
                // Nếu món vừa chọn có rule -> Parse ra Tag. Nếu không -> null.
                string? requiredTag = ParseTagFromRule(selectedOption.NextStepFilterRule, nextStepName);

                var nextOptions = await _kitRepo.GetCompatibleOptionsForStepAsync(session.BaseKitId, nextStepName, requiredTag);
                nextProducts = _mapper.Map<List<CompatiblePartDto>>(nextOptions);
            }

            return ConstructResponse(session, nextStepName, nextStepOrder, nextProducts);
        }

        // --- HELPER FUNCTIONS ---

        private BuilderStepResponse ConstructResponse(BuilderSession session, string nextStepName, int nextOrder, List<CompatiblePartDto> products)
        {
            var selectionDict = JsonSerializer.Deserialize<Dictionary<string, SelectedPartDetail>>(session.SelectedItemsJson);

            return new BuilderStepResponse
            {
                Message = "Step updated",
                Data = new BuilderSessionData
                {
                    Session = new SessionInfo
                    {
                        Id = session.Id,
                        TotalPrice = session.TotalPrice,
                        UpdatedAt = DateTime.UtcNow,
                        Selection = selectionDict,
                        IsComplete = nextStepName == "complete"
                    },
                    NextStep = new NextStepInfo
                    {
                        Step = new StepDetail { Name = nextStepName, Slug = nextStepName, StepOrder = nextOrder },
                        Products = products
                    }
                }
            };
        }

        // Logic Hard-code thứ tự các bước (Có thể thay bằng query DB nếu muốn dynamic hơn)
        private (string Name, int Order) GetNextStepInfo(string currentStep)
        {
            return currentStep.ToLower() switch
            {
                "case" => ("plate", 2),
                "plate" => ("switch", 3),
                "switch" => ("keycap", 4),
                "keycap" => ("complete", 5), // Hết bước
                _ => ("complete", 99)
            };
        }

        // Parse Rule: "plate:LAYOUT_65" -> trả về "LAYOUT_65"
        private string? ParseTagFromRule(string? rule, string targetStep)
        {
            if (string.IsNullOrEmpty(rule)) return null;
            var parts = rule.Split(':');
            if (parts.Length == 2 && parts[0].ToLower() == targetStep.ToLower())
            {
                return parts[1]; // Trả về Tag
            }
            return null;
        }
    }
}
 