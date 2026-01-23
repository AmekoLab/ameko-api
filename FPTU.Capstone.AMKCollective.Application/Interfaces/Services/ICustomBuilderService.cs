using FPTU.Capstone.AMKCollective.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ICustomBuilderService
    {
        Task<BuilderConfigDto> GetBuilderConfigAsync(Guid baseKitId);
        Task<(IEnumerable<CompatiblePartDto> Items, int TotalCount)> SearchPartsInBuilderAsync(CompatiblePartsQuery query);
        Task<bool> ValidateConfigurationAsync(Guid baseKitId, List<Guid> componentIds);//check before add to cart
        Task CreateOptionAsync(CreateKitOptionDto request);
        Task DeleteOptionAsync(Guid id);
        Task BulkCreateOptionsAsync(List<CreateKitOptionDto> requests);

        Task ResetBuilderConfigAsync(Guid baseKitId);
        Task<bool> IsMatchAsync(Guid baseKitId, Guid componentId);


        //SERVER-DRIVEN FLOW
        // 1. Bắt đầu phiên Build
        Task<BuilderStepResponse> StartBuilderSessionAsync(BuilderStartRequest request);

        // 2. Chọn linh kiện và lấy bước tiếp theo
        Task<BuilderStepResponse> SelectPartAsync(BuilderSelectRequest request);
    }
}
