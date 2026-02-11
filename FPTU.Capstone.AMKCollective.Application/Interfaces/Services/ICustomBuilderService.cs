using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ICustomBuilderService
    {
        Task<BuilderConfigResponse> GetBuilderConfigAsync(Guid baseKitId);
        Task<(IEnumerable<CompatiblePartResponse> Items, int TotalCount)> SearchPartsInBuilderAsync(GetCompatiblePartsRequest query);
        Task<bool> ValidateConfigurationAsync(Guid baseKitId, List<Guid> componentIds);//check before add to cart
        Task CreateOptionAsync(CreateKitOptionRequest request);
        Task DeleteOptionAsync(Guid id);
        Task BulkCreateOptionsAsync(List<CreateKitOptionRequest> requests);

        Task ResetBuilderConfigAsync(Guid baseKitId);
        Task<bool> IsMatchAsync(Guid baseKitId, Guid componentId);

        Task<BuilderStepResponse> RemovePartFromSessionAsync(Guid sessionId, string stepName);
        //SERVER-DRIVEN FLOW
        // 1. Bắt đầu phiên Build
        Task<BuilderStepResponse> StartBuilderSessionAsync(BuilderStartRequest request, Guid? userId);

        // 2. Chọn linh kiện và lấy bước tiếp theo
        Task<BuilderStepResponse> SelectPartAsync(BuilderSelectRequest request);

        Task<BuilderStepResponse> GetExistingSessionAsync(Guid sessionId, string? requestStep = null);
        Task<List<BuilderSessionSummaryResponse>> GetUserSessionsAsync(Guid userId);
        Task<Guid> CreateSessionFromOrderAsync(Guid orderItemId, Guid userId);
    }
}
