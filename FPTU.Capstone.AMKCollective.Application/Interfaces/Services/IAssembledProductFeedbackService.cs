using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback;
using System;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IAssembledProductFeedbackService
    {
        Task<AssembledProductFeedbackResponse> CreateAsync(Guid userId, Guid orderItemId, CreateAssembledProductFeedbackRequest request);
        Task<AssembledProductFeedbackResponse> UpdateAsync(Guid userId, Guid feedbackId, UpdateAssembledProductFeedbackRequest request);
        Task<AssembledProductFeedbackResponse> ReplyAsync(Guid shopUserId, Guid feedbackId, ReplyAssembledProductFeedbackRequest request);
        Task<PaginatedResult<AssembledProductFeedbackResponse>> GetByProductIdAsync(Guid productId, int pageNumber, int pageSize);
        Task<PaginatedResult<AssembledProductFeedbackResponse>> GetMyShopFeedbacksAsync(Guid userId, int pageNumber, int pageSize);
        Task<AssembledProductFeedbackEligibilityResponse> GetEligibilityByProductIdAsync(Guid userId, Guid productId);
    }
}
