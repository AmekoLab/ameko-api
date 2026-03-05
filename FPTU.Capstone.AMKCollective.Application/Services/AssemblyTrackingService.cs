using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AssemblyTrackingService : IAssemblyTrackingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public AssemblyTrackingService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
        }
        #region Template Management

        public async Task<IEnumerable<AssemblyStepTemplateResponse>> GetTemplatesByShopIdAsync(Guid shopId)
        {
            var templates = await _unitOfWork.AssemblyStepTemplates.GetTemplatesByShopIdAsync(shopId);
            return _mapper.Map<IEnumerable<AssemblyStepTemplateResponse>>(templates);
        }

        public async Task<AssemblyStepTemplateResponse> CreateTemplateAsync(Guid shopId, SaveAssemblyStepTemplateRequest request)
        {
            var template = _mapper.Map<AssemblyStepTemplate>(request);
            template.ShopId = shopId;

            await _unitOfWork.AssemblyStepTemplates.AddAsync(template);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyStepTemplateResponse>(template);
        }

        public async Task<AssemblyStepTemplateResponse> UpdateTemplateAsync(Guid templateId, SaveAssemblyStepTemplateRequest request)
        {
            // Do entity của bạn dùng ID là int, mình giả định lấy bằng int (Nếu bạn đã sửa BaseEntity thành Guid thì code này vẫn chạy đúng)
            var template = await _unitOfWork.AssemblyStepTemplates.GetByIdAsync(templateId);

            if (template == null)
                throw new KeyNotFoundException("Không tìm thấy mẫu quy trình này.");

            _mapper.Map(request, template);

            _unitOfWork.AssemblyStepTemplates.Update(template);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyStepTemplateResponse>(template);
        }

        public async Task DeleteTemplateAsync(Guid templateId)
        {
            var template = await _unitOfWork.AssemblyStepTemplates.GetByIdAsync(templateId);

            if (template == null)
                throw new KeyNotFoundException("Không tìm thấy mẫu quy trình này.");

            _unitOfWork.AssemblyStepTemplates.Delete(template);
            await _unitOfWork.CommitAsync();
        }

        #endregion

        #region Tracking Logs Management

        public async Task<IEnumerable<AssemblyProgressLogResponse>> GetTrackingLogsAsync(Guid orderItemId)
        {
            var logs = await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(orderItemId);
            return _mapper.Map<IEnumerable<AssemblyProgressLogResponse>>(logs);
        }

        public async Task<AssemblyProgressLogResponse> UpdateProgressLogAsync(Guid progressLogId, UpdateAssemblyProgressRequest request)
        {
            var log = await _unitOfWork.AssemblyProgressLogs.GetByIdAsync(progressLogId);

            if (log == null)
                throw new KeyNotFoundException("Không tìm thấy bước tiến trình này.");

            log.Status = request.Status;
            log.Note = request.Note;

            if (request.Status == AssemblyStepStatus.Completed && log.CompletedAt == null)
            {
                log.CompletedAt = DateTime.UtcNow;
            }

            // Xử lý File đính kèm nếu có
            if (request.MediaFile != null && request.MediaFile.Length > 0)
            {
                using var stream = request.MediaFile.OpenReadStream();
                string uploadedUrl = await _storageService.UploadAsync(stream, request.MediaFile.FileName, "tracking");
                log.MediaUrl = uploadedUrl;
            }

            _unitOfWork.AssemblyProgressLogs.Update(log);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyProgressLogResponse>(log);
        }

        public async Task<AssemblyProgressLogResponse> AddAdhocStepAsync(Guid orderItemId, AddAdhocStepRequest request)
        {
            var log = new AssemblyProgressLog
            {
                OrderItemId = orderItemId,
                StepName = request.StepName,
                StepOrder = request.StepOrder,
                Note = request.Note,
                Status = AssemblyStepStatus.Pending
            };

            if (request.MediaFile != null && request.MediaFile.Length > 0)
            {
                using var stream = request.MediaFile.OpenReadStream();
                string uploadedUrl = await _storageService.UploadAsync(stream, request.MediaFile.FileName, "tracking");
                log.MediaUrl = uploadedUrl;
            }

            await _unitOfWork.AssemblyProgressLogs.AddAsync(log);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyProgressLogResponse>(log);
        }

        #endregion

        #region Internal Triggers

        public async Task GenerateTrackingLogsForOrderItemAsync(Guid orderItemId, Guid shopId)
        {
            var templates = await _unitOfWork.AssemblyStepTemplates.GetTemplatesByShopIdAsync(shopId);

            if (!templates.Any()) return;

            var logsToCreate = templates.Select(t => new AssemblyProgressLog
            {
                OrderItemId = orderItemId,
                StepName = t.StepName,
                StepOrder = t.StepOrder,
                Status = AssemblyStepStatus.Pending
            }).ToList();

            await _unitOfWork.AssemblyProgressLogs.AddRangeAsync(logsToCreate);
            await _unitOfWork.CommitAsync();
        }

        #endregion
    }
}
   