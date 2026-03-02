using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IOrderIssueService
    {
        // 1. Customer
        Task<OrderIssueResponse?> GetIssueByOrderIdAsync(Guid userId, Guid orderId, CancellationToken token = default);
        Task<PaginatedResult<OrderIssueResponse>> GetMyIssuesAsync(Guid userId, OrderIssueFilterRequest request, CancellationToken token = default);

        // 2. Shop
        Task<PaginatedResult<OrderIssueResponse>> GetShopIssuesAsync(Guid shopId, OrderIssueFilterRequest request, CancellationToken token = default);

        // 3.
        Task<OrderIssueResponse> GetIssueDetailAsync(Guid userId, Guid issueId, CancellationToken token = default);
        Task<List<OrderIssueLogResponse>> GetIssueLogsAsync(Guid userId, Guid issueId, CancellationToken token = default);
    }
}
