using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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
        private readonly BuilderSettings _builderSettings;

        public CustomBuilderService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storageService, IOptions<BuilderSettings> builderOptions)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
            _builderSettings = builderOptions.Value;
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
        public async Task CreateOptionAsync(CreateKitOptionRequest request, Guid callerUserId, bool isAdmin)
        {
            await EnsureKitOwnershipAsync(request.BaseKitId, callerUserId, isAdmin);

            // Ánh xạ dữ liệu cơ bản từ request sang entity
            var entity = _mapper.Map<KitDesignOption>(request);

            // ==========================================
            // XỬ LÝ ẢNH 3 TẦNG
            // ==========================================

            // 1. Ưu TIÊN 1: Nếu có file mới -> Upload lên Cloudinary
            if (request.LayerImageFile != null && request.LayerImageFile.Length > 0)
            {
                var fileUrl = await _storageService.UploadAsync(
                    request.LayerImageFile.OpenReadStream(),
                    request.LayerImageFile.FileName,
                    "builder-layers"
                );

                entity.LayerImageUrl = fileUrl;
            }
            // 2. Nếu không có file mới, nhưng có gửi link cũ lên, Giữ nguyên link ảnh
            else if (!string.IsNullOrEmpty(request.ExistingLayerUrl))
            {
                entity.LayerImageUrl = request.ExistingLayerUrl;
            }
            // Ưu tiên 3: Nếu cả 2 đều trống thì để mặc định theo logic gốc

            // ==========================================

            // Tạo ID mới nếu chưa có
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();

            // Lưu vào Database
            await _unitOfWork.KitDesignOptions.CreateAsync(entity);
            await _unitOfWork.CommitAsync();

            await ReevaluateBuilderReadinessAsync(entity.BaseKitId);
        }

        public async Task DeleteOptionAsync(Guid id, Guid callerUserId, bool isAdmin)
        {
            var option = await _unitOfWork.KitDesignOptions.GetByIdAsync(id);
            if (option == null) throw new KeyNotFoundException("Kit design option not found.");

            await EnsureKitOwnershipAsync(option.BaseKitId, callerUserId, isAdmin);

            await _unitOfWork.KitDesignOptions.DeleteAsync(id);
            await _unitOfWork.CommitAsync();

            await ReevaluateBuilderReadinessAsync(option.BaseKitId);
        }

        public async Task BulkCreateOptionsAsync(List<CreateKitOptionRequest> requests, Guid callerUserId, bool isAdmin)
        {
            if (!requests.Any()) return;

            var distinctKitIds = requests.Select(r => r.BaseKitId).Distinct().ToList();
            if (distinctKitIds.Count > 1)
                throw new ArgumentException("All options in a bulk request must belong to the same BaseKitId.");

            await EnsureKitOwnershipAsync(distinctKitIds[0], callerUserId, isAdmin);

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

                await ReevaluateBuilderReadinessAsync(entitiesToInsert.First().BaseKitId);
            }
        }

        public async Task BatchSaveOptionsAsync(List<BatchKitOptionItem> items, Guid callerUserId, bool isAdmin)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("Batch list cannot be empty.");

            await EnsureKitOwnershipAsync(items.First().BaseKitId, callerUserId, isAdmin);

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.StepName))
                    throw new ArgumentException($"StepName is required for ComponentId {item.ComponentId}.");
            }

            // Duplicate = cùng ComponentId + StepName + Tags trong 1 batch
            var duplicates = items
                .GroupBy(o => (o.ComponentId, o.StepName.Trim(), o.Tags?.Trim()))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                var first = duplicates.First();
                throw new ArgumentException(
                    $"Duplicate rule detected: ComponentId={first.ComponentId}, StepName={first.Item2}, Tags={first.Item3 ?? "null"}");
            }

            var baseKitId = items.First().BaseKitId;

            var newOptions = items.Select(o => new KitDesignOption
            {
                Id = Guid.NewGuid(),
                BaseKitId = o.BaseKitId,
                ComponentId = o.ComponentId,
                StepName = o.StepName.Trim(),
                StepOrder = o.StepOrder,
                IsDefault = o.IsDefault,
                LayerImageUrl = string.IsNullOrWhiteSpace(o.ExistingLayerUrl) ? null : o.ExistingLayerUrl,
                Tags = string.IsNullOrWhiteSpace(o.Tags) ? null : o.Tags,
                NextStepFilterRule = string.IsNullOrWhiteSpace(o.NextStepFilterRule) ? null : o.NextStepFilterRule,
            }).ToList();

            await _unitOfWork.ExecuteTransactionAsync(async () =>
            {
                await _unitOfWork.KitDesignOptions.DeleteByBaseKitAsync(baseKitId);
                await _unitOfWork.KitDesignOptions.CreateBatchAsync(newOptions);
                await _unitOfWork.CommitAsync();
            });

            await ReevaluateBuilderReadinessAsync(baseKitId);
        }

        public async Task<string> UploadLayerImageAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return await _storageService.UploadAsync(stream, file.FileName, "builder-layers");
        }

        public async Task ResetBuilderConfigAsync(Guid baseKitId, Guid callerUserId, bool isAdmin)
        {
            bool exists = await _unitOfWork.Models.ExistsAsync(baseKitId);
            if (!exists) throw new KeyNotFoundException("Base Kit not found");

            await EnsureKitOwnershipAsync(baseKitId, callerUserId, isAdmin);

            await _unitOfWork.KitDesignOptions.DeleteByBaseKitAsync(baseKitId);
            await _unitOfWork.CommitAsync();

            await _unitOfWork.Models.UpdateIsBuilderReadyAsync(baseKitId, false);
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

            if (!baseKit.IsBuilderReady)
                throw new InvalidOperationException("Kit này chưa sẵn sàng để build. Shop đang cấu hình linh kiện.");

            //  Lấy workflow động từ JSON
            var workflow = GetWorkflowFromKit(baseKit);
            var firstStep = workflow.FirstOrDefault();

            if (firstStep == null)
            {
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
                ExpiresAt = DateTime.UtcNow.AddHours(_builderSettings.SessionExpirationHours)
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

        public async Task<BuilderStepResponse> SelectPartAsync(BuilderSelectRequest request, Guid? userId)
        {
            // 1. Lấy Session
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.SessionId);
            if (session == null) throw new KeyNotFoundException("Session expired or not found");
            if (session.UserId.HasValue && session.UserId != userId)
                throw new UnauthorizedAccessException("Access denied to this session.");

            if (session.ExpiresAt < DateTime.UtcNow)
                throw new KeyNotFoundException("Session has expired. Please start a new session.");

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
            // Check tồn kho
            int qtyNeeded = workflow[currentStepIndex].Quantity; // Lấy số lượng cần thiết từ cấu hình workflow

            if (selectedOption.Component.StockQuantity < qtyNeeded)
            {
                throw new InvalidOperationException($"Component '{selectedOption.Component.Name}' is currently out of stock (Available: {selectedOption.Component.StockQuantity}, Required: {qtyNeeded}). Please choose another component.");
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
             qtyNeeded = workflow[currentStepIndex].Quantity;

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


        public async Task<BuilderStepResponse> RemovePartFromSessionAsync(Guid sessionId, string stepName, Guid? userId)
        {
            // 1. Lấy Session
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(sessionId);
            if (session == null || session.ExpiresAt < DateTime.UtcNow)
            {
                throw new KeyNotFoundException("Session not found or expired");
            }
            if (session.UserId.HasValue && session.UserId != userId)
                throw new UnauthorizedAccessException("Access denied to this session.");

            // 2. Parse JSON hiện tại
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartResponse>();

            // 3. Kiểm tra xem bước cần xóa có tồn tại trong selection không
            if (currentSelection.ContainsKey(stepName))
            {
                // Xóa item tại bước đó
                currentSelection.Remove(stepName);

                // Lấy quy trình workflow để biết thứ tự các bước
                var workflow = GetWorkflowFromKit(session.BaseKit);
                var currentStepIndex = workflow.FindIndex(w => w.Step.Equals(stepName, StringComparison.OrdinalIgnoreCase));

                // Nếu tìm thấy bước trong workflow, xóa tất cả các bước SAU bước này
                // Lý do: Nếu xóa Case (bước 1), các lựa chọn Plate (bước 2) có thể không còn khớp tag tương thích nữa.
                if (currentStepIndex != -1)
                {
                    for (int i = currentStepIndex + 1; i < workflow.Count; i++)
                    {
                        currentSelection.Remove(workflow[i].Step);
                    }
                }
            }

            // 4. Tính lại tổng tiền
            decimal newTotal = session.BaseKit.Price;
            foreach (var item in currentSelection.Values)
            {
                newTotal += (item.Price * item.Quantity);
            }
            session.TotalPrice = newTotal;

            // 5. Đưa người dùng quay lại bước vừa xóa để chọn lại
            session.CurrentStep = stepName;

            // 6. Cập nhật và lưu DB
            session.SelectedItemsJson = JsonSerializer.Serialize(currentSelection);
            await _unitOfWork.BuilderSessions.UpdateSessionAsync(session);
            await _unitOfWork.CommitAsync();

            // 7. Lấy lại danh sách tùy chọn (Products) cho bước này để trả về FE
            // Cần tính toán lại Tag lọc từ bước trước đó (vì bước hiện tại đã bị xóa)
            string? requiredTag = null;
            var workflowList = GetWorkflowFromKit(session.BaseKit);
            var stepIndex = workflowList.FindIndex(w => w.Step.Equals(stepName, StringComparison.OrdinalIgnoreCase));
            int stepOrder = (stepIndex == -1) ? 99 : stepIndex + 1;

            if (stepIndex > 0)
            {
                var prevStepName = workflowList[stepIndex - 1].Step;
                if (currentSelection.TryGetValue(prevStepName, out var prevPart))
                {
                    // Tái sử dụng hàm helper có sẵn
                    if (!string.IsNullOrEmpty(prevPart.NextStepFilterRule))
                    {
                        requiredTag = ParseTagFromRule(prevPart.NextStepFilterRule, stepName);
                    }
                    // Fallback nếu trong JSON chưa lưu Rule (tương thích ngược)
                    else if (prevPart.KitDesignOptionId != Guid.Empty)
                    {
                        var prevOption = await _unitOfWork.KitDesignOptions.GetByIdAsync(prevPart.KitDesignOptionId);
                        if (prevOption != null)
                        {
                            requiredTag = ParseTagFromRule(prevOption.NextStepFilterRule, stepName);
                        }
                    }
                }
            }

            var availableProducts = await _unitOfWork.KitDesignOptions.GetCompatibleOptionsForStepAsync(
                session.BaseKitId, stepName, requiredTag);

            var productDtos = _mapper.Map<List<CompatiblePartResponse>>(availableProducts);

            // 8. Trả về kết quả (Hàm ConstructResponse sẽ tự tính toán lại ảnh Preview lùi về bước trước)
            return ConstructResponse(session, stepName, stepOrder, productDtos);
        }

        public async Task<List<BuilderSessionSummaryResponse>> GetUserSessionsAsync(Guid userId)
        {
            // 1. Lấy danh sách từ Repo
            var sessions = await _unitOfWork.BuilderSessions.GetActiveSessionsByUserIdAsync(userId);
            var result = new List<BuilderSessionSummaryResponse>();

            foreach (var session in sessions)
            {
                // 2. Xử lý ảnh Preview
                // Mặc định lấy ảnh thumbnail của Kit
                string previewImage = session.BaseKit.ThumbnailURL ?? "";

                // Nếu muốn hiển thị ảnh custom chính xác (ảnh linh kiện cuối cùng đã chọn)
                // Ta cần parse JSON nhẹ nhàng
                if (!string.IsNullOrEmpty(session.SelectedItemsJson) && session.SelectedItemsJson != "{}")
                {
                    try
                    {
                        var selectionDict = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson);
                        if (selectionDict != null && selectionDict.Any())
                        {
                            // Logic tìm ảnh mới nhất giống hàm ConstructResponse
                            var workflow = GetWorkflowFromKit(session.BaseKit);
                            for (int i = workflow.Count - 1; i >= 0; i--)
                            {
                                var stepName = workflow[i].Step;
                                if (selectionDict.TryGetValue(stepName, out var part) && !string.IsNullOrEmpty(part.LayerImageUrl))
                                {
                                    previewImage = part.LayerImageUrl;
                                    break;
                                }
                            }
                        }
                    }
                    catch { /* Ignore json error for summary list */ }
                }

                // 3. Map sang DTO
                result.Add(new BuilderSessionSummaryResponse
                {
                    SessionId = session.Id,
                    BaseKitId = session.BaseKitId,
                    BaseKitName = session.BaseKit.Name,
                    BaseKitThumbnail = session.BaseKit.ThumbnailURL,
                    CurrentStep = session.CurrentStep,
                    TotalPrice = session.TotalPrice,
                    CurrentPreviewImage = previewImage,
                    CreatedAt = session.CreatedAt,
                    UpdatedAt = session.UpdatedAt ?? session.CreatedAt,
                    ExpiresAt = session.ExpiresAt
                });
            }

            return result;
        }

        public async Task<BuilderStepResponse> GetExistingSessionAsync(Guid sessionId, string? requestStep = null, Guid? userId = null)
        {
            // 1. Validate Session
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                throw new KeyNotFoundException("Session not found");
            }
            if (session.UserId.HasValue && userId.HasValue && session.UserId != userId)
                throw new UnauthorizedAccessException("Access denied to this session.");

            if (session.ExpiresAt < DateTime.UtcNow)
                throw new KeyNotFoundException("Session expired");

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

        public async Task<Guid> CreateSessionFromOrderAsync(Guid orderItemId, Guid userId)
        {
            // 1. Lấy thông tin Order Item và các linh kiện con
            // Lưu ý: Cần đảm bảo Repo Order lấy cả OrderItemComponents (Include)
            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(orderItemId);
            if (orderItem == null) throw new KeyNotFoundException("Order item not found");
            if (orderItem.Order?.CustomerId != userId)
                throw new UnauthorizedAccessException("This order item does not belong to you.");
            if (!orderItem.IsCustom || orderItem.ProductId == null) throw new InvalidOperationException("Not a custom kit");

            // 2. Lấy cấu hình gốc của Kit để map ngược ComponentId -> StepName (VD: ID 123 -> "case")
            var baseKitId = orderItem.ProductId.Value;
            var allOptions = await _unitOfWork.KitDesignOptions.GetOptionsByBaseKitAsync(baseKitId);

            // 3. Tái tạo Dictionary cho Session
            var selection = new Dictionary<string, SelectedPartResponse>();

            foreach (var comp in orderItem.OrderItemComponents)
            {
                // Tìm xem linh kiện này thuộc Step nào (VD: Case hay Plate?)
                var option = allOptions.FirstOrDefault(x => x.ComponentId == comp.PartId);

                // Nếu tìm thấy option khớp, add vào dictionary
                if (option != null)
                {
                    selection[option.StepName] = new SelectedPartResponse
                    {
                        Id = comp.PartId,
                        Name = comp.PartName ?? "",
                        Price = comp.PartPriceSnapshot, // Hoặc lấy giá mới nhất từ bảng Model nếu muốn
                        ThumbnailUrl = comp.PartImageUrl,
                        Quantity = comp.Quantity,

                        // Các trường phục vụ logic nhánh
                        KitDesignOptionId = option.Id,
                        LayerImageUrl = option.LayerImageUrl ?? "",
                        NextStepFilterRule = option.NextStepFilterRule
                    };
                }
            }

            // 4. Tạo Session Mới (State là Complete để FE load lên là xong luôn)
            var newSession = new BuilderSession
            {
                Id = Guid.NewGuid(),
                BaseKitId = baseKitId,
                UserId = userId,
                CurrentStep = "complete", // Đánh dấu là đã hoàn thành
                SelectedItemsJson = JsonSerializer.Serialize(selection),
                TotalPrice = orderItem.UnitPrice, // Lấy giá tại thời điểm mua hoặc tính lại
                ExpiresAt = DateTime.UtcNow.AddHours(_builderSettings.SessionExpirationHours),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.BuilderSessions.CreateSessionAsync(newSession);
            await _unitOfWork.CommitAsync();

            return newSession.Id;
        }

        public async Task<(bool Success, Guid? CommissionRequestId, string ErrorMessage)> ConvertSessionToCommissionAsync(Guid userId, BuilderToCommissionRequest request)
        {
            // 1. Lấy Builder Session lên để kiểm tra
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.SessionId);
            if (session == null || session.UserId != userId)
                return (false, null, "Builder session not found or access denied.");

            if (session.ExpiresAt < DateTime.UtcNow)
                return (false, null, "Builder session has expired. Please start a new session.");

            // SelectedItemsJson được serialize là Dictionary<string, SelectedPartResponse> — không phải List<SelectedPartDto>
            // Deserialization sai type cũ luôn trả về null → function luôn fail
            var selectionDict = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson);
            if (selectionDict == null || !selectionDict.Any())
                return (false, null, "No parts have been selected in this session.");

            // Dùng session.BaseKit trực tiếp thay vì tìm trong selectedParts
            // BaseKit đã được Include sẵn bởi GetSessionByIdAsync
            var kitModel = session.BaseKit;
            if (kitModel == null)
                return (false, null, "Could not find the base kit for this session.");

            // Kiểm tra tồn kho của từng part được chọn trong session
            var workflow = GetWorkflowFromKit(kitModel);
            foreach (var step in workflow)
            {
                if (!selectionDict.TryGetValue(step.Step, out var selectedPart)) continue;

                var component = await _unitOfWork.Models.GetByIdAsync(selectedPart.Id);
                if (component == null)
                    return (false, null, $"Component '{selectedPart.Name}' no longer exists. Please restart the builder.");

                if (component.StockQuantity < selectedPart.Quantity)
                    return (false, null, $"Component '{selectedPart.Name}' is out of stock (Available: {component.StockQuantity}, Required: {selectedPart.Quantity}). Please choose another component.");
            }

            // Tính tổng tiền vật tư cơ bản
            decimal baseMaterialPrice = selectionDict.Values.Sum(p => p.Price * p.Quantity);

            var newCommission = new CommissionRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TargetedShopId = kitModel.ShopId, // Chỉ đích danh cái Shop bán Base Kit
                Title = $"Custom Build: {kitModel.Name}",

                // Dùng selectionDict.Values thay vì selectedParts 
                Description = $"Customer's Special Request:\n{request.CustomerNote}\n\n--- Base Material Configuration (Total: {baseMaterialPrice:N0} VND) ---\n"
                              + JsonSerializer.Serialize(selectionDict, new JsonSerializerOptions { WriteIndented = true }),

                MinBudget = baseMaterialPrice,
                MaxBudget = 0,
                Status = CommissionStatus.PendingTarget,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.CommissionRequests.AddAsync(newCommission);

            // 4. Xóa BuilderSession cũ bằng hàm Async vì đã chuyển hóa thành công
            await _unitOfWork.BuilderSessions.DeleteSessionAsync(session.Id);

            // Lưu toàn bộ thay đổi xuống Database
            await _unitOfWork.CommitAsync();

            return (true, newCommission.Id, string.Empty);
        }

        public async Task<BuilderStepResponse> AddExtraPartToSessionAsync(BuilderAddonRequest request, Guid? userId)
        {
            // 1. Basic Validations
            if (request.SessionId == Guid.Empty)
                throw new ArgumentException("Session ID cannot be empty.");

            if (request.Items == null || !request.Items.Any())
                return await GetExistingSessionAsync(request.SessionId); // Không có gì để add thì trả về luôn

            // 2. Fetch Session
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(request.SessionId);
            if (session == null)
                throw new KeyNotFoundException($"Builder session with ID {request.SessionId} not found.");
            if (session.UserId.HasValue && session.UserId != userId)
                throw new UnauthorizedAccessException("Access denied to this session.");
            if (session.ExpiresAt < DateTime.UtcNow)
                throw new KeyNotFoundException("Session has expired. Please start a new session.");
            if (session.BaseKit == null)
                throw new InvalidOperationException("BaseKit information is missing for this session.");

            // 3. Process Add-ons
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartResponse>();

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                    throw new ArgumentException("Quantity must be greater than zero.");

                var extraPart = await _unitOfWork.Models.GetByIdAsync(item.ComponentId);
                if (extraPart == null)
                    throw new KeyNotFoundException($"Add-on component with ID {item.ComponentId} not found.");

                // Stock check — không cho add addon khi hết hàng
                if (extraPart.StockQuantity < item.Quantity)
                    throw new InvalidOperationException(
                        $"Add-on '{extraPart.Name}' does not have enough stock (Available: {extraPart.StockQuantity}, Required: {item.Quantity}). Please choose another quantity or component.");

                // Lấy category slug để phân biệt switch vs keycap tại cùng vị trí.
                // Dùng PartType của component; fallback về "part" nếu null.
                string categorySlug = extraPart.PartType?.ToLower().Trim().Replace(" ", "_") ?? "part";

                // Key luôn bao gồm cả categorySlug lẫn ComponentId/PositionNote để
                // đảm bảo switch và keycap tại CÙNG vị trí KHÔNG bao giờ ghi đè nhau.
                // VD: addon_switch_a  vs  addon_keycap_a  → 2 key khác nhau.
                string addonKey = string.IsNullOrWhiteSpace(item.PositionNote)
                    ? $"addon_{categorySlug}_comp_{item.ComponentId}"
                    : $"addon_{categorySlug}_{item.PositionNote.Trim().ToLower().Replace(" ", "_")}";

                string displayName = string.IsNullOrWhiteSpace(item.PositionNote)
                    ? extraPart.Name
                    : $"{extraPart.Name} (Position: {item.PositionNote})";

                // Fix switch DOUBLE-COUNT:
                // - Switch replacement: lưu baseUnitPriceToDeduct = giá per-unit switch cũ
                //   → khi cộng giá chỉ cộng phần CHÊNH LỆCH (newPrice - oldPrice)
                // - Keycap artisan (extra thật): baseUnitPriceToDeduct luôn = 0
                decimal baseUnitPriceToDeduct = 0;

                if (!string.IsNullOrWhiteSpace(item.ReplacesStepName))
                {
                    // Kiểm tra xem component này có phải keycap không.
                    // Keycap artisan KHÔNG phải replacement dù FE có gửi ReplacesStepName.
                    bool isKeycapCategory = categorySlug.Contains("keycap");

                    if (!isKeycapCategory)
                    {
                        // Switch (hoặc part khác): chỉ deduct nếu step bị replace thực sự tồn tại
                        var stepToReplace = item.ReplacesStepName.Trim().ToLower();
                        if (currentSelection.TryGetValue(stepToReplace, out var baseStep) && baseStep.Quantity > 0)
                        {
                            baseUnitPriceToDeduct = baseStep.Price; // per-unit price của switch cũ
                        }
                    }
                    // isKeycapCategory == true → baseUnitPriceToDeduct giữ nguyên = 0
                }

                currentSelection[addonKey] = new SelectedPartResponse
                {
                    Id = extraPart.Id,
                    Name = displayName,
                    Price = extraPart.Price,
                    ThumbnailUrl = extraPart.ThumbnailURL ?? "",
                    Quantity = item.Quantity,
                    KitDesignOptionId = Guid.Empty,
                    LayerImageUrl = "",
                    NextStepFilterRule = "",
                    BaseUnitPriceToDeduct = baseUnitPriceToDeduct
                };
            }

            // 4. Recalculate Total Price (Tính lại giá tự động)
            decimal newTotal = session.BaseKit.Price;
            foreach (var kvp in currentSelection)
            {
                var part = kvp.Value;
                if (kvp.Key.StartsWith("addon_"))
                {
                    // Addon: cộng giá mới, trừ giá cũ của linh kiện bị replace
                    // Nếu BaseUnitPriceToDeduct = 0 (keycap artisan) → cộng toàn bộ
                    // Nếu BaseUnitPriceToDeduct > 0 (switch replacement) → chỉ cộng phần chênh lệch
                    newTotal += (part.Price - part.BaseUnitPriceToDeduct) * part.Quantity;
                }
                else
                {
                    // Builder step bình thường
                    newTotal += part.Price * part.Quantity;
                }
            }

            // 5. Save Changes
            session.TotalPrice = newTotal;
            session.SelectedItemsJson = JsonSerializer.Serialize(currentSelection);

            await _unitOfWork.BuilderSessions.UpdateSessionAsync(session);
            await _unitOfWork.CommitAsync();

            return await GetExistingSessionAsync(session.Id, session.CurrentStep);
        }

        public async Task<BuilderStepResponse> RemoveExtraPartFromSessionAsync(Guid sessionId, string addonKey, Guid? userId)
        {
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(sessionId);
            if (session == null) throw new KeyNotFoundException("Session expired or not found");
            if (session.UserId.HasValue && session.UserId != userId)
                throw new UnauthorizedAccessException("Access denied to this session.");
            if (session.ExpiresAt < DateTime.UtcNow)
                throw new KeyNotFoundException("Session has expired. Please start a new session.");
            var currentSelection = JsonSerializer.Deserialize<Dictionary<string, SelectedPartResponse>>(session.SelectedItemsJson)
                                   ?? new Dictionary<string, SelectedPartResponse>();

            // Kiểm tra và xóa Addon nếu tồn tại
            if (currentSelection.ContainsKey(addonKey))
            {
                currentSelection.Remove(addonKey);

                // Tính lại tiền — dùng cùng logic với AddExtraPartToSessionAsync:
                // Addon key (bắt đầu bằng "addon_"): cộng (Price - BaseUnitPriceToDeduct) * Quantity
                // Builder step bình thường: cộng Price * Quantity
                decimal newTotal = session.BaseKit.Price;
                foreach (var kvp in currentSelection)
                {
                    var part = kvp.Value;
                    if (kvp.Key.StartsWith("addon_"))
                    {
                        newTotal += (part.Price - part.BaseUnitPriceToDeduct) * part.Quantity;
                    }
                    else
                    {
                        newTotal += part.Price * part.Quantity;
                    }
                }

                session.TotalPrice = newTotal;
                session.SelectedItemsJson = JsonSerializer.Serialize(currentSelection);

                await _unitOfWork.BuilderSessions.UpdateSessionAsync(session);
                await _unitOfWork.CommitAsync();
            }

            // Trả về Session mới nhất để FE cập nhật lại giao diện
            return await GetExistingSessionAsync(session.Id, session.CurrentStep);
        }

        // ========================= ADDON OPTIONS ======================================

        /// <summary>
        /// Lấy danh sách linh kiện có thể add-on khi click vào phím trên bàn phím ảo.
        /// addonType (optional): chỉ còn là PartType filter phụ.
        ///   Không truyền / "all" → trả toàn bộ addon-eligible parts của shop.
        /// </summary>
        public async Task<AddonOptionsResponse> GetAddonOptionsAsync(
            Guid sessionId, string addonType, Guid? userId, string? searchTerm = null, int page = 1, int pageSize = 20)
        {
            var session = await _unitOfWork.BuilderSessions.GetSessionByIdAsync(sessionId);
            if (session == null)
                throw new KeyNotFoundException($"Builder session {sessionId} not found.");

            var shopId = session.BaseKit?.ShopId;
            if (shopId == null || shopId == Guid.Empty)
                throw new InvalidOperationException("Cannot determine shop from session's base kit.");

            // addonType chỉ còn là PartType filter tuỳ chọn — không route sang datasource khác nữa
            string? partTypeFilter = string.IsNullOrWhiteSpace(addonType) || addonType.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : addonType.Trim();

            var (parts, total) = await _unitOfWork.Models.GetPagedAsync(new GetPartsFilterRequest
            {
                ShopId = shopId,
                IsAddonEligible = true,    // Nguồn sự thật duy nhất — shop đã tick flag khi tạo sản phẩm
                IsActive = true,
                PartType = partTypeFilter,
                SearchTerm = searchTerm,
                PageNumber = page,
                PageSize = pageSize
            });

            return new AddonOptionsResponse
            {
                AddonType = partTypeFilter ?? "all",
                TotalCount = total,
                Items = parts.Select(p => new AddonOptionItem
                {
                    PartId = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    ThumbnailUrl = p.ThumbnailURL,
                    PartType = p.PartType,    // FE dùng để nhóm/hiển thị switch/keycap
                    StockQuantity = p.StockQuantity,
                    KitDesignOptionId = null  // không route qua KitDesignOption nữa
                }).ToList()
            };
        }

        public async Task UpdateOptionAsync(Guid id, UpdateKitOptionRequest request, Guid callerUserId, bool isAdmin)
        {
            // 1. Tìm Option cũ trong DB
            var entity = await _unitOfWork.KitDesignOptions.GetByIdAsync(id);
            if (entity == null) throw new KeyNotFoundException("Kit Design Option not found");

            await EnsureKitOwnershipAsync(entity.BaseKitId, callerUserId, isAdmin);

            // 2. Dùng AutoMapper đè dữ liệu mới vào entity cũ (tự động bỏ qua các trường null)
            _mapper.Map(request, entity);

            // 3. Xử lý riêng cái file ảnh (nếu có upload ảnh mới)
            if (request.LayerImageFile != null && request.LayerImageFile.Length > 0)
            {
                var fileUrl = await _storageService.UploadAsync(
                    request.LayerImageFile.OpenReadStream(),
                    request.LayerImageFile.FileName,
                    "builder-layers"
                );
                entity.LayerImageUrl = fileUrl;
            }

            // 4. Lưu xuống DB
            await _unitOfWork.KitDesignOptions.UpdateAsync(entity);
            await _unitOfWork.CommitAsync();
        }

        // --- HELPER FUNCTIONS ---

        // 0. Ownership guard: Admin bypass, Shop chỉ được thao tác trên kit thuộc shop mình
        private async Task EnsureKitOwnershipAsync(Guid baseKitId, Guid callerUserId, bool isAdmin)
        {
            if (isAdmin) return;

            var kit = await _unitOfWork.Models.GetByIdAsync(baseKitId);
            if (kit == null) throw new KeyNotFoundException("Base Kit not found.");

            // kit.Shop phải được Include bởi GetByIdAsync — nếu null thì deny (safe default)
            if (kit.Shop?.UserId != callerUserId)
                throw new UnauthorizedAccessException("You do not own this kit configuration.");
        }

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

        // DEAD CODE — Commented out: function này dùng step names hard-code ("case", "plate", "switch")
        // và không được call ở bất kỳ đâu. Dynamic workflow đã thay thế hoàn toàn.
        // private SelectedPartResponse? GetPreviousSelectedPart(Dictionary<string, SelectedPartResponse> selection, string currentStep)
        // {
        //     if (currentStep == "plate" && selection.ContainsKey("case")) return selection["case"];
        //     if (currentStep == "switch" && selection.ContainsKey("plate")) return selection["plate"];
        //     if (currentStep == "keycap" && selection.ContainsKey("switch")) return selection["switch"];
        //     return null;
        // }

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

        /// Tính lại IsBuilderReady sau mỗi lần thay đổi KitDesignOption.
        /// Ready = tất cả bước trong workflow đều có ít nhất 1 option.
        private async Task ReevaluateBuilderReadinessAsync(Guid baseKitId)
        {
            var kit = await _unitOfWork.Models.GetByIdAsync(baseKitId);
            if (kit == null) return;

            var workflowSteps = GetWorkflowFromKit(kit)
                .Select(s => s.Step)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var configuredSteps = (await _unitOfWork.KitDesignOptions.GetOptionsByBaseKitAsync(baseKitId))
                .Select(o => o.StepName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool isReady = workflowSteps.Count > 0 && workflowSteps.All(s => configuredSteps.Contains(s));

            await _unitOfWork.Models.UpdateIsBuilderReadyAsync(baseKitId, isReady);
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