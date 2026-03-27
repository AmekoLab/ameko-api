using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IAssemblyTrackingService
    {
        // Quản lý Template của Shop
        Task<IEnumerable<AssemblyStepTemplateResponse>> GetTemplatesByShopIdAsync(Guid shopId);
        Task<AssemblyStepTemplateResponse> CreateTemplateAsync(Guid shopId, SaveAssemblyStepTemplateRequest request);
        Task<AssemblyStepTemplateResponse> UpdateTemplateAsync(Guid templateId, Guid shopId, SaveAssemblyStepTemplateRequest request);
        Task DeleteTemplateAsync(Guid templateId, Guid shopId);

        // Quản lý Tiến trình (Tracking Logs)
        Task<IEnumerable<AssemblyProgressLogResponse>> GetTrackingLogsAsync(Guid orderItemId, Guid requestingUserId);
        Task<AssemblyProgressLogResponse> UpdateProgressLogAsync(Guid progressLogId, Guid shopId, UpdateAssemblyProgressRequest request);
        Task<AssemblyProgressLogResponse> AddAdhocStepAsync(Guid orderItemId, Guid shopId, AddAdhocStepRequest request);

        Task GenerateTrackingLogsForOrderItemAsync(Guid orderItemId, Guid shopId);
        Task DeleteProgressLogAsync(Guid progressLogId, Guid shopId);
    }
}
