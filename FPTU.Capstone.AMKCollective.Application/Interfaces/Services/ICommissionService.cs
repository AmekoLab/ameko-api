using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ICommissionService
    {
        // User
        Task<IEnumerable<CommissionRequestResponse>> GetUserRequestsAsync(Guid userId);
        Task<CommissionRequestResponse?> GetRequestDetailAsync(Guid requestId, Guid currentUserId);
        Task<(bool Success, Guid? RequestId, string ErrorMessage)> CreateRequestAsync(Guid userId, CreateCommissionRequest request);
        Task<(bool Success, string ErrorMessage)> UpdateRequestAsync(Guid userId, Guid requestId, UpdateCommissionRequest request);
        Task<(bool Success, Guid? OrderId, string ErrorMessage)> AcceptQuoteAsync(Guid userId, Guid quoteId);
        Task<(bool Success, string ErrorMessage)> RejectQuoteAsync(Guid userId, Guid quoteId);
        Task<(bool Success, string ErrorMessage)> CancelRequestAsync(Guid userId, Guid requestId);
        Task<(bool Success, string ErrorMessage)> PublishToPoolAsync(Guid userId, Guid requestId);
        // Shop
        Task<IEnumerable<CommissionRequestResponse>> GetOpenPoolRequestsAsync(Guid? currentUserId = null);
        Task<(bool Success, string ErrorMessage)> SubmitQuoteAsync(Guid shopUserId, Guid requestId, SubmitQuoteRequest request);
        Task<(bool Success, string ErrorMessage)> UpdateQuoteAsync(Guid shopUserId, Guid quoteId, SubmitQuoteRequest updateRequest);
        Task<(bool Success, string ErrorMessage)> RevokeQuoteAsync(Guid shopUserId, Guid quoteId);
        Task<IEnumerable<CommissionRequestResponse>> GetShopTargetedRequestsAsync(Guid shopUserId);
        Task<IEnumerable<CommissionQuoteResponse>> GetShopQuotesAsync(Guid shopUserId);
        Task<(bool Success, string ErrorMessage)> RejectTargetedRequestAsync(Guid shopUserId, Guid requestId);
        Task ProcessCommissionRemindersAsync(CancellationToken cancellationToken = default);
    }
}
