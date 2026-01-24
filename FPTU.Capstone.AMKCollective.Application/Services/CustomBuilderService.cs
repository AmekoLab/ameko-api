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


        public async Task<BuilderStepResponse> StartBuilderSessionAsync(BuilderStartRequest request, Guid? userId)
        {
            // 1. Validate Base Kit
            var baseKit = await _modelRepo.GetByIdAsync(request.BaseKitId);
            if (baseKit == null) throw new KeyNotFoundException("Base Kit not found");
            BuilderSession session;

            if (userId.HasValue)
            {
                var existingSession = await _sessionRepo.GetActiveSessionByUserIdAsync(userId.Value, request.BaseKitId);

                if (existingSession != null)
                {
                    // Nếu có -> Trả về session cũ luôn (Tính năng Sync)
                    return await GetExistingSessionAsync(existingSession.Id);
                }
            }
            // 2. Tạo Session Mới
            session = new BuilderSession
            {
                Id = Guid.NewGuid(),
                BaseKitId = request.BaseKitId,
                UserId = userId, // <--- LƯU USER ID VÀO ĐÂY
                CurrentStep = "case",
                SelectedItemsJson = "{}",
                TotalPrice = baseKit.Price,
                ExpiresAt = DateTime.UtcNow.AddHours(48) // Tăng thời gian lên 48h
            };

            await _sessionRepo.CreateSessionAsync(session);
            // 3. Lấy linh kiện cho bước đầu tiên (Case)
            var firstStepOptions = await _kitRepo.GetCompatibleOptionsForStepAsync(request.BaseKitId, "case", null);

            // 4. Trả về Response
            return ConstructResponse(session, "case", 1, _mapper.Map<List<CompatiblePartDto>>(firstStepOptions));
        }

        public async Task<BuilderStepResponse> SelectPartAsync(BuilderSelectRequest request)
        {
            // 1. Lấy Session
            var session = await _sessionRepo.GetSessionByIdAsync(request.SessionId);
            if (session == null) throw new KeyNotFoundException("Session expired or not found");

            // 2. Validate: Lấy thông tin linh kiện vừa chọn
            // (Lưu ý: Dùng null ở tham số requiredTag vì ta đang validate cái user chọn, ko phải lọc list)
            var stepOptions = await _kitRepo.GetCompatibleOptionsForStepAsync(session.BaseKitId, request.StepName, null);
            var selectedOption = stepOptions.FirstOrDefault(x => x.ComponentId == request.SelectedPartId);

            if (selectedOption == null) throw new KeyNotFoundException("Selected part is not valid for this kit");

            // 3. Update Session
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartDetail>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartDetail>();

            // Ví dụ: Đang sửa bước 'case', thì phải xóa 'plate', 'switch' cũ đi
            ClearSubsequentSteps(currentSelection, request.StepName);
            // ----------------------------------------------------------

            // Lưu món vừa chọn vào Dictionary
            currentSelection[request.StepName] = new SelectedPartDetail
            {
                Id = selectedOption.ComponentId,
                Name = selectedOption.Component.Name,
                Price = selectedOption.Component.Price,
                ThumbnailUrl = selectedOption.Component.ThumbnailURL
            };

            // Tính lại tổng tiền
            decimal newTotal = session.BaseKit.Price;
            foreach (var item in currentSelection.Values)
            {
                newTotal += item.Price;
            }

            session.SelectedItemsJson = JsonSerializer.Serialize(currentSelection);
            session.TotalPrice = newTotal;

            // 4. Xác định Bước Tiếp Theo
            // Logic: Sau khi chọn xong bước này thì nhảy sang bước kế tiếp
            var (nextStepName, nextStepOrder) = GetNextStepInfo(request.StepName);

            session.CurrentStep = nextStepName; // Cập nhật bước hiện tại của user là bước tiếp theo
            await _sessionRepo.UpdateSessionAsync(session);

            // 5. Lấy danh sách sản phẩm cho bước BƯỚC TIẾP THEO (có Lọc Tag)
            List<CompatiblePartDto> nextProducts = new();
            if (nextStepName != "complete")
            {
                // Lấy rule từ món VỪA CHỌN để lọc cho món KẾ TIẾP
                // Ví dụ: Vừa chọn Case 65% (rule: plate:LAYOUT_65) -> Lọc Plate theo tag LAYOUT_65
                string? requiredTag = ParseTagFromRule(selectedOption.NextStepFilterRule, nextStepName);

                var nextOptions = await _kitRepo.GetCompatibleOptionsForStepAsync(session.BaseKitId, nextStepName, requiredTag);
                nextProducts = _mapper.Map<List<CompatiblePartDto>>(nextOptions);
            }

            return ConstructResponse(session, nextStepName, nextStepOrder, nextProducts);
        }
        public async Task<BuilderStepResponse> GetExistingSessionAsync(Guid sessionId, string? requestStep = null)
        {
            // 1. Tìm Session
            var session = await _sessionRepo.GetSessionByIdAsync(sessionId);
            if (session == null || session.ExpiresAt < DateTime.UtcNow)
            {
                throw new KeyNotFoundException("Session not found or expired");
            }

            // 2. Parse dữ liệu
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartDetail>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartDetail>();

            // 3. Xác định bước cần hiển thị dữ liệu (Quan Trọng)
            // Nếu FE gửi requestStep (ví dụ user click tab "case") -> Dùng requestStep
            // Nếu không -> Dùng CurrentStep đang lưu trong DB (resume nơi đang làm dở)
            var stepToProcess = requestStep ?? session.CurrentStep;

            // Lấy Order để hiển thị (1, 2, 3...)
            var (_, stepOrder) = GetNextStepInfo(stepToProcess);

            // 4. Logic lọc (Filter Logic) cho bước stepToProcess
            string? requiredTag = null;

            // Tìm xem BƯỚC TRƯỚC ĐÓ đã chọn cái gì để lấy Rule
            var previousPart = GetPreviousSelectedPart(currentSelection, stepToProcess);

            if (previousPart != null)
            {
                // Gọi DB lấy Rule của món cũ
                var prevOption = await _kitRepo.GetOptionByComponentIdAsync(session.BaseKitId, previousPart.Id);

                if (prevOption != null)
                {
                    requiredTag = ParseTagFromRule(prevOption.NextStepFilterRule, stepToProcess);
                }
            }

            // 5. Query DB lấy sản phẩm (đã lọc)
            var availableProducts = await _kitRepo.GetCompatibleOptionsForStepAsync(session.BaseKitId, stepToProcess, requiredTag);
            var productDtos = _mapper.Map<List<CompatiblePartDto>>(availableProducts);

            // 6. Trả về Response
            return ConstructResponse(session, stepToProcess, stepOrder, productDtos);
        }
        // --- HELPER FUNCTIONS ---

        // 1. Logic thứ tự các bước (Hard-code)
        private (string Name, int Order) GetNextStepInfo(string currentStep)
        {
            return currentStep.ToLower() switch
            {
                "case" => ("plate", 2),
                "plate" => ("switch", 3),
                "switch" => ("keycap", 4),
                "keycap" => ("complete", 5),
                _ => ("complete", 99)
            };
        }

        // 2. Logic tìm món ở bước ngay trước đó
        private SelectedPartDetail? GetPreviousSelectedPart(Dictionary<string, SelectedPartDetail> selection, string currentStep)
        {
            // Nếu đang ở bước 'plate', thì bước trước là 'case'. Kiểm tra xem đã chọn case chưa.
            if (currentStep == "plate" && selection.ContainsKey("case")) return selection["case"];

            if (currentStep == "switch" && selection.ContainsKey("plate")) return selection["plate"];

            if (currentStep == "keycap" && selection.ContainsKey("switch")) return selection["switch"];

            return null; // Case là bước đầu, không có bước trước
        }

        // 3. Logic xóa các bước phía sau (Khi user chọn lại từ đầu)
        private void ClearSubsequentSteps(Dictionary<string, SelectedPartDetail> selection, string currentStep)
        {
            if (currentStep == "case")
            {
                selection.Remove("plate");
                selection.Remove("switch");
                selection.Remove("keycap");
            }
            else if (currentStep == "plate")
            {
                selection.Remove("switch");
                selection.Remove("keycap");
            }
            else if (currentStep == "switch")
            {
                selection.Remove("keycap");
            }
        }

        // 4. Parse Rule 
        private string? ParseTagFromRule(string? rule, string targetStep)
        {
            if (string.IsNullOrEmpty(rule)) return null;
            var parts = rule.Split(':');
            if (parts.Length == 2 && parts[0].ToLower() == targetStep.ToLower())
            {
                return parts[1];
            }
            return null;
        }

        // 5. Construct Response (Giữ nguyên)
        private BuilderStepResponse ConstructResponse(BuilderSession session, string nextStepName, int nextOrder, List<CompatiblePartDto> products)
        {
            var selectionDict = JsonSerializer.Deserialize<Dictionary<string, SelectedPartDetail>>(session.SelectedItemsJson);

            return new BuilderStepResponse
            {
                Message = "Success",
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
    }
}