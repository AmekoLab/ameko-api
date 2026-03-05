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
        Task<AssemblyStepTemplateResponse> UpdateTemplateAsync(Guid templateId, SaveAssemblyStepTemplateRequest request);
        Task DeleteTemplateAsync(Guid templateId); 

        // Quản lý Tiến trình (Tracking Logs)
        Task<IEnumerable<AssemblyProgressLogResponse>> GetTrackingLogsAsync(Guid orderItemId);
        Task<AssemblyProgressLogResponse> UpdateProgressLogAsync(Guid progressLogId, UpdateAssemblyProgressRequest request);
        Task<AssemblyProgressLogResponse> AddAdhocStepAsync(Guid orderItemId, AddAdhocStepRequest request);

        // Hàm gọi nội bộ (Trigger)
        Task GenerateTrackingLogsForOrderItemAsync(Guid orderItemId, Guid shopId);
    }
}
