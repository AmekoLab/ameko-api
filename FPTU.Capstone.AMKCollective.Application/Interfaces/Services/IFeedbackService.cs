using FPTU.Capstone.AMKCollective.Application.DTOs.Feedback;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IFeedbackService
    {
        Task<FeedbackResponse> CreateFeedbackAsync(Guid userId, Guid orderId, CreateFeedbackRequest request);
        Task<FeedbackResponse> ReplyFeedbackAsync(Guid shopUserId, Guid feedbackId, ReplyFeedbackRequest request);
        Task<IEnumerable<FeedbackResponse>> GetShopFeedbacksAsync(Guid shopId);
    }
}
