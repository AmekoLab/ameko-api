using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
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
    public class CustomBuilderService : ICustomBuilderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public CustomBuilderService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<BuilderConfigResponse> GetBuilderConfigAsync(Guid baseKitId)
        {
            var baseKit = await _unitOfWork.Models.GetByIdAsync(baseKitId);
            if (baseKit == null) throw new KeyNotFoundException("Base Kit not found");

            var options = await _unitOfWork.KitDesignOptions.GetOptionsByBaseKitAsync(baseKitId);

            var steps = options
                .GroupBy(x => new { x.StepName, x.StepOrder })
                .Select(g => new BuilderStepConfigResponse
                {
                    StepName = g.Key.StepName,
                    StepOrder = g.Key.StepOrder,
                    PartType = g.First().Component.PartType ?? "UNKNOWN",
                    Options = _mapper.Map<List<CompatiblePartResponse>>(g.ToList())
                })
                .OrderBy(s => s.StepOrder)
                .ToList();
            return new BuilderConfigResponse
            {
                BaseKitId = baseKit.Id,
                BaseKitName = baseKit.Name,
                BaseThumbnail = baseKit.ThumbnailURL ?? "",
                Steps = steps
            };
        }
        public async Task<(IEnumerable<CompatiblePartResponse> Items, int TotalCount)> SearchPartsInBuilderAsync(GetCompatiblePartsRequest query)
        {
            var (entities, total) = await _unitOfWork.KitDesignOptions.GetCompatiblePartsPagedAsync(query);
            var dtos = _mapper.Map<IEnumerable<CompatiblePartResponse>>(entities);

            return (dtos, total);
        }
        public async Task<bool> ValidateConfigurationAsync(Guid baseKitId, List<Guid> componentIds)
        {
            if (componentIds == null || !componentIds.Any()) return false;
            var validIds = await _unitOfWork.KitDesignOptions.GetValidComponentIdsAsync(baseKitId, componentIds);
            return validIds.Count() == componentIds.Count;
        }
        public async Task CreateOptionAsync(CreateKitOptionRequest request)
        {
            var entity = _mapper.Map<KitDesignOption>(request);

            if (request.LayerImageFile != null && request.LayerImageFile.Length > 0)
            {
                var fileUrl = await _storageService.UploadAsync(
                    request.LayerImageFile.OpenReadStream(),
                    request.LayerImageFile.FileName,
                    "builder-layers"
                );

                entity.LayerImageUrl = fileUrl;
            }
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();

            await _unitOfWork.KitDesignOptions.CreateAsync(entity);
            await _unitOfWork.CommitAsync();
        }

        public async Task DeleteOptionAsync(Guid id)
        {
            await _unitOfWork.KitDesignOptions.DeleteAsync(id);
            await _unitOfWork.CommitAsync();
        }

        public async Task BulkCreateOptionsAsync(List<CreateKitOptionRequest> requests)
        {
            var entitiesToInsert = new List<KitDesignOption>();
            foreach (var req in requests)
            {
                var entity = _mapper.Map<KitDesignOption>(req);
                if (req.LayerImageFile != null && req.LayerImageFile.Length > 0)
                {
                    var fileUrl = await _storageService.UploadAsync(
                        req.LayerImageFile.OpenReadStream(),
                        req.LayerImageFile.FileName,
                        "builder-layers"
                    );
                    entity.LayerImageUrl = fileUrl;
                }
                if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
                entitiesToInsert.Add(entity);
            }

            if (entitiesToInsert.Any())
            {
                await _unitOfWork.KitDesignOptions.CreateBatchAsync(entitiesToInsert);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task ResetBuilderConfigAsync(Guid baseKitId)
        {
            bool exists = await _unitOfWork.Models.ExistsAsync(baseKitId);
            if (!exists) throw new KeyNotFoundException("Base Kit not found");

            await _unitOfWork.KitDesignOptions.DeleteByBaseKitAsync(baseKitId);
            await _unitOfWork.CommitAsync();
        }
        public async Task<bool> IsMatchAsync(Guid baseKitId, Guid componentId)
        {
            return await _unitOfWork.KitDesignOptions.CheckCompatibilityAsync(baseKitId, componentId);
        }

        // ===================================SERVER-DRIVEN BUILDER======================================//


        public async Task<BuilderStepResponse> StartBuilderSessionAsync(BuilderStartRequest request, Guid? userId)
        {
            // 1. Validate Base Kit
            var baseKit = await _unitOfWork.Models.GetByIdAsync(request.BaseKitId);
            if (baseKit == null) throw new KeyNotFoundException("Base Kit not found");

            //  Lấy workflow động từ JSON
            var workflow = GetWorkflowFromKit(baseKit);
            var firstStep = workflow.FirstOrDefault();

            if (firstStep == null)
            {
                // Fallback: nếu không tìm thấy bước nào, báo lỗi 
                throw new InvalidOperationException("Kit configuration is invalid (no steps defined in Specifications).");
            }
            // ---------------------------------------------

            BuilderSession session;

            //if (userId.HasValue)
            //{
            //    var existingSession = await _unitOfWork.BuilderSessions.GetActiveSessionByUserIdAsync(userId.Value, request.BaseKitId);

            //    if (existingSession != null)
            //    {
            //        // Nếu có session cũ -> Trả về (Tính năng Sync)
            //        return await GetExistingSessionAsync(existingSession.Id);
            //    }
            //}

            // 2. Tạo Session Mới với bước đầu tiên động
            session = new BuilderSession
            {
                Id = Guid.NewGuid(),
                BaseKitId = request.BaseKitId,
                UserId = userId,
                CurrentStep = firstStep.Step, // <--Dùng biến động, không hard-code "case"
                SelectedItemsJson = "{}",
                TotalPrice = baseKit.Price,
                ExpiresAt = DateTime.UtcNow.AddHours(48)
            };

            await _unitOfWork.BuilderSessions.CreateSessionAsync(session);
            await _unitOfWork.CommitAsync();

            // 3. Lấy linh kiện cho bước đầu tiên (Dựa trên firstStep.Step)
            // Bước đầu tiên thường chưa có tag lọc (null)
            var firstStepOptions = await _unitOfWork.KitDesignOptions.GetCompatibleOptionsForStepAsync(
                request.BaseKitId,
                firstStep.Step,
                null
            );

            // 4. Trả về Response         
            return ConstructResponse(
                session,
                firstStep.Step,
                1, // StepOrder mặc định là 1 cho bước đầu
                _mapper.Map<List<CompatiblePartResponse>>(firstStepOptions)
            );
        }

        public async Task<BuilderStepResponse> SelectPartAsync(BuilderSelectRequest request)
        {
            // 1. Lấy Session
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.SessionId);
            if (session == null) throw new KeyNotFoundException("Session expired or not found");

            // 2. Lấy danh sách quy trình động từ BaseKit
            var workflow = GetWorkflowFromKit(session.BaseKit);

            // Tìm vị trí của bước hiện tại trong quy trình
            var currentStepIndex = workflow.FindIndex(w => w.Step.Equals(request.StepName, StringComparison.OrdinalIgnoreCase));

            if (currentStepIndex == -1)
            {
                throw new ArgumentException($"Step '{request.StepName}' is not valid for this Kit configuration.");
            }

            // 3. Phục hồi các linh kiện đã chọn trước đó
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartResponse>();

            // 4. Lấy tag lọc của bước hiện tại (để đảm bảo user chọn đúng món thuộc nhánh đã đi)
            // Logic: Tìm món ở bước NGAY TRƯỚC bước hiện tại để lấy Rule
            string? currentStepTag = null;
            if (currentStepIndex > 0) // Nếu không phải bước đầu tiên
            {
                var prevStepName = workflow[currentStepIndex - 1].Step;
                if (currentSelection.TryGetValue(prevStepName, out var prevPart))
                {
                    currentStepTag = ParseTagFromRule(prevPart.NextStepFilterRule, request.StepName);
                }
            }

            // 5. Query DB để lấy món user vừa chọn (Validate xem có hợp lệ với nhánh không)
            var stepOptions = await _unitOfWork.KitDesignOptions.GetCompatibleOptionsForStepAsync(
                session.BaseKitId, request.StepName, currentStepTag);

            var selectedOption = stepOptions.FirstOrDefault(x => x.ComponentId == request.SelectedPartId);
            if (selectedOption == null)
            {
                throw new KeyNotFoundException("Selected part is not valid for this kit or current branch.");
            }

            // 6. Xóa các bước phía sau (Nếu user quay lại sửa bước cũ -> clear các bước sau để chọn lại)
            // Logic: Duyệt từ index hiện tại + 1 đến hết list và xóa khỏi session
            for (int i = currentStepIndex + 1; i < workflow.Count; i++)
            {
                currentSelection.Remove(workflow[i].Step);
            }

            // 7. Lưu lựa chọn vào Session
            // Ưu tiên lấy ảnh Layer từ Option (ảnh tích lũy), nếu không có thì lấy ảnh mặc định của Component
            string resolvedLayerImage = !string.IsNullOrEmpty(selectedOption.LayerImageUrl)
                ? selectedOption.LayerImageUrl
                : selectedOption.Component.DefaultLayerImageUrl ?? "";

            // Lấy số lượng từ cấu hình Workflow
            int qtyNeeded = workflow[currentStepIndex].Quantity;

            currentSelection[request.StepName] = new SelectedPartResponse
            {
                Id = selectedOption.ComponentId,
                Name = selectedOption.Component.Name,
                Price = selectedOption.Component.Price,
                ThumbnailUrl = selectedOption.Component.ThumbnailURL,
                Quantity = qtyNeeded,

                // Lưu các thông tin phục vụ điều hướng nhánh
                KitDesignOptionId = selectedOption.Id,
                LayerImageUrl = resolvedLayerImage,         // <--- Ảnh này sẽ được FE hiển thị
                NextStepFilterRule = selectedOption.NextStepFilterRule // <--- Rule dẫn tới bước sau
            };

            // 8. Tính lại tổng tiền
            decimal newTotal = session.BaseKit.Price;
            foreach (var item in currentSelection.Values)
            {
                newTotal += (item.Price * item.Quantity);
            }

            // 9. Xác định bước tiếp theo
            string nextStepName = "complete";
            int nextStepOrder = currentStepIndex + 2; // Order hiển thị = index + 1, nên Next = index + 2

            if (currentStepIndex < workflow.Count - 1)
            {
                nextStepName = workflow[currentStepIndex + 1].Step;
            }

            // 10. Cập nhật và Lưu Session
            session.SelectedItemsJson = JsonSerializer.Serialize(currentSelection);
            session.TotalPrice = newTotal;
            session.CurrentStep = nextStepName; // Cập nhật bước hiện tại của session

            await _unitOfWork.BuilderSessions.UpdateSessionAsync(session);
            await _unitOfWork.CommitAsync();

            // 11. Chuẩn bị dữ liệu linh kiện cho bước tiếp theo (Pre-fetch)
            List<CompatiblePartResponse> nextProducts = new();
            if (nextStepName != "complete")
            {
                // Lấy Rule từ món vừa chọn để lọc cho bước sau
                // Ví dụ: Vừa chọn Case Đen -> Rule "plate:case-den" -> Lọc Plate có tag "case-den"
                string? nextRequiredTag = ParseTagFromRule(selectedOption.NextStepFilterRule, nextStepName);

                var nextOptions = await _unitOfWork.KitDesignOptions.GetCompatibleOptionsForStepAsync(
                    session.BaseKitId, nextStepName, nextRequiredTag);

                nextProducts = _mapper.Map<List<CompatiblePartResponse>>(nextOptions);
            }

            return ConstructResponse(session, nextStepName, nextStepOrder, nextProducts);
        }

        public async Task<BuilderStepResponse> GetExistingSessionAsync(Guid sessionId, string? requestStep = null)
        {
            // 1. Validate Session
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(sessionId);
            if (session == null || session.ExpiresAt < DateTime.UtcNow)
            {
                throw new KeyNotFoundException("Session not found or expired");
            }

            // 2. Lấy Workflow
            var workflow = GetWorkflowFromKit(session.BaseKit);
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartResponse>();

            // 3. Xác định bước cần hiển thị (Step to process)
            string stepToProcess = requestStep ?? session.CurrentStep;

            // Tìm index của bước này trong workflow
            int stepIndex = workflow.FindIndex(w => w.Step.Equals(stepToProcess, StringComparison.OrdinalIgnoreCase));
            int stepOrder = (stepIndex == -1) ? 99 : stepIndex + 1; // Order để hiển thị FE

            // 4. Xác định Tag lọc (Branching Logic)
            string? requiredTag = null;

            // Nếu không phải bước đầu tiên, tìm bước liền trước nó để lấy Rule
            if (stepIndex > 0)
            {
                var prevStepName = workflow[stepIndex - 1].Step;
                if (currentSelection.TryGetValue(prevStepName, out var prevPart))
                {
                    // Ưu tiên 1: Lấy từ NextStepFilterRule đã lưu trong Session (Nhanh nhất)
                    if (!string.IsNullOrEmpty(prevPart.NextStepFilterRule))
                    {
                        requiredTag = ParseTagFromRule(prevPart.NextStepFilterRule, stepToProcess);
                    }
                    // Ưu tiên 2: Fallback vào DB nếu Session cũ chưa lưu Rule (Hỗ trợ tương thích ngược)
                    else if (prevPart.KitDesignOptionId != Guid.Empty)
                    {
                        var prevOption = await _unitOfWork.KitDesignOptions.GetByIdAsync(prevPart.KitDesignOptionId);
                        if (prevOption != null)
                        {
                            requiredTag = ParseTagFromRule(prevOption.NextStepFilterRule, stepToProcess);
                        }
                    }
                }
            }

            // 5. Query DB lấy sản phẩm phù hợp với nhánh
            var availableProducts = await _unitOfWork.KitDesignOptions.GetCompatibleOptionsForStepAsync(
                session.BaseKitId, stepToProcess, requiredTag);

            var productDtos = _mapper.Map<List<CompatiblePartResponse>>(availableProducts);

            // 6. Trả về kết quả
            return ConstructResponse(session, stepToProcess, stepOrder, productDtos);
        }
        // --- HELPER FUNCTIONS ---

        // 1. Logic thứ tự các bước (Hard-code)
        //private (string Name, int Order) GetNextStepInfo(string currentStep)
        //{
        //    return currentStep.ToLower() switch
        //    {
        //        "case" => ("plate", 2),
        //        "plate" => ("switch", 3),
        //        "switch" => ("keycap", 4),
        //        "keycap" => ("complete", 5),
        //        _ => ("complete", 99)
        //    };
        //}

        // 2. Logic tìm món ở bước ngay trước đó
        private SelectedPartResponse? GetPreviousSelectedPart(Dictionary<string, SelectedPartResponse> selection, string currentStep)
        {
            // Nếu đang ở bước 'plate', thì bước trước là 'case'. Kiểm tra xem đã chọn case chưa.
            if (currentStep == "plate" && selection.ContainsKey("case")) return selection["case"];

            if (currentStep == "switch" && selection.ContainsKey("plate")) return selection["plate"];

            if (currentStep == "keycap" && selection.ContainsKey("switch")) return selection["switch"];

            return null; // Case là bước đầu, không có bước trước
        }

        // 3. Logic xóa các bước phía sau (Khi user chọn lại từ đầu)
        //private void ClearSubsequentSteps(Dictionary<string, SelectedPartResponse> selection, string currentStep)
        //{
        //    if (currentStep == "case")
        //    {
        //        selection.Remove("plate");
        //        selection.Remove("switch");
        //        selection.Remove("keycap");
        //    }
        //    else if (currentStep == "plate")
        //    {
        //        selection.Remove("switch");
        //        selection.Remove("keycap");
        //    }
        //    else if (currentStep == "switch")
        //    {
        //        selection.Remove("keycap");
        //    }
        //}

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

        // 5. Construct Response (Nhánh hình ảnh tích lũy)
        private BuilderStepResponse ConstructResponse(BuilderSession session, string nextStepName, int nextOrder, List<CompatiblePartResponse> products)
        {
            var selectionDict = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson);

            // 1. Logic tìm ảnh tích lũy mới nhất để hiển thị
            string? currentPreviewImage = null;

            // Lấy workflow để biết thứ tự ưu tiên (bước sau đè bước trước)
            var workflow = GetWorkflowFromKit(session.BaseKit);

            if (selectionDict != null && selectionDict.Any())
            {
                // Duyệt ngược từ cuối workflow lên đầu để tìm ảnh mới nhất
                for (int i = workflow.Count - 1; i >= 0; i--)
                {
                    var stepName = workflow[i].Step;
                    if (selectionDict.TryGetValue(stepName, out var part) && !string.IsNullOrEmpty(part.LayerImageUrl))
                    {
                        currentPreviewImage = part.LayerImageUrl;
                        break; // Tìm thấy ảnh của bước xa nhất đã chọn -> Dùng làm ảnh preview
                    }
                }
            }

            // 2. Trả về Response
            return new BuilderStepResponse
            {
                Message = "Success",
                Data = new BuilderSessionData
                {
                    Session = new SessionInfoResponse
                    {
                        Id = session.Id,
                        TotalPrice = session.TotalPrice,
                        UpdatedAt = DateTime.UtcNow,
                        Selection = selectionDict,
                        IsComplete = nextStepName == "complete",
                        CurrentPreviewImage = currentPreviewImage // <--- Ảnh này đã được xử lý theo nhánh
                    },
                    NextStep = new NextStepResponse
                    {
                        Step = new BuilderStepDetailResponse
                        {
                            Name = nextStepName,
                            Slug = nextStepName,
                            StepOrder = nextOrder
                        },
                        Products = products
                    },
                     WorkflowSteps = workflow.Select(w => w.Step).ToList() 
                }
            };
        }

        private int GetRecipeQuantity(string? specifications, string? categorySlug)
        {
            if (string.IsNullOrEmpty(specifications) || string.IsNullOrEmpty(categorySlug)) return 1;

            try
            {
                using (var doc = JsonDocument.Parse(specifications))
                {
                    if (doc.RootElement.TryGetProperty("recipe", out var recipe))
                    {
                        foreach (var prop in recipe.EnumerateObject())
                        {
                            if (categorySlug.ToLower() == prop.Name.ToLower())
                            {
                                return prop.Value.GetInt32();
                            }
                        }
                    }
                }
            }
            catch { }
            return 1; 
        }

        private List<KitWorkflowStep> GetWorkflowFromKit(Model baseKit)
        {
            // 1. Cấu hình mặc định (Fallback)
            var defaultSteps = new List<KitWorkflowStep>
    {
        new() { Step = "case", Title = "Case", Quantity = 1 },
        new() { Step = "plate", Title = "Plate", Quantity = 1 },
        new() { Step = "switch", Title = "Switch", Quantity = 1 }, // Số lượng switch sẽ được tính lại sau nếu cần
        new() { Step = "keycap", Title = "Keycap", Quantity = 1 },
        new() { Step = "stabilizer", Title = "Stabilizer", Quantity = 1 }
    };

            if (string.IsNullOrEmpty(baseKit.Specifications)) return defaultSteps;

            try
            {
                // 2. Cố gắng đọc từ JSON trong DB
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var spec = JsonSerializer.Deserialize<KitSpecificationSchema>(baseKit.Specifications, options);

                // 3. Nếu JSON có dữ liệu workflow hợp lệ thì dùng nó
                if (spec != null && spec.Workflow != null && spec.Workflow.Any())
                {
                    return spec.Workflow;
                }
            }
            catch
            {
                
            }

            return defaultSteps;
        }
    }
}