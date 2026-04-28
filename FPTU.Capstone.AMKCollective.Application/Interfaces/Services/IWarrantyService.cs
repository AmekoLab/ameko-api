using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IWarrantyService
    {
        // ... (keeping other methods as is for brevity in this tool call, but ensuring the interface is correct)
        
        /// <summary>
        /// Phase 1: Customer creates a warranty/return issue.
        /// </summary>
        Task<IEnumerable<WarrantyIssueResponse>> CreateWarrantyIssueAsync(Guid userId, Guid orderGroupId, CreateWarrantyIssueDto? dto, CancellationToken ct = default);

        /// <summary>
        /// Update an existing warranty request (only if InProgress).
        /// </summary>
        Task<WarrantyIssueResponse> UpdateWarrantyIssueAsync(Guid userId, Guid issueId, UpdateWarrantyIssueDto dto, CancellationToken ct = default);

        /// <summary>
        /// Soft-delete (cancel) a warranty request (only if InProgress).
        /// </summary>
        Task DeleteWarrantyIssueAsync(Guid userId, Guid issueId, CancellationToken ct = default);

        /// <summary>
        /// Phase 2: Shop owner reviews the issue (approve/reject).
        /// </summary>
        Task ShopReviewWarrantyAsync(Guid shopOwnerId, ShopWarrantyResponseDto dto, CancellationToken ct = default);

        /// <summary>
        /// Phase 3: Admin makes a decision. System auto-determines return vs refund based on Order state.
        /// </summary>
        Task AdminDecisionWarrantyAsync(Guid adminId, AdminWarrantyDecisionDto dto, CancellationToken ct = default);

        /// <summary>
        /// Phase 4a: Customer confirms return shipment with evidence.
        /// </summary>
        Task CustomerConfirmShipmentAsync(Guid userId, ReturnWarrantyShipmentDto dto, CancellationToken ct = default);

        /// <summary>
        /// Phase 4b: Shop confirms receipt of returned product.
        /// </summary>
        Task ShopConfirmReceiveAsync(Guid shopOwnerId, Guid issueId, CancellationToken ct = default);
        
        /// <summary>
        /// Phase 4c: Shop disputes the returned item (e.g., fake item).
        /// </summary>
        Task ShopDisputeReturnAsync(Guid shopOwnerId, ShopWarrantyResponseDto dto, CancellationToken ct = default);

        /// <summary>
        /// Background job: auto-cancel issues in AwaitingReturn for more than 3 days.
        /// </summary>
        Task AutoCancelExpiredIssuesAsync(CancellationToken ct = default);

        /// <summary>
        /// Admin: retrieves a paginated list of all warranty requests.
        /// </summary>
        Task<FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>> GetAllWarrantyIssuesAsync(OrderIssueStatus? status, int currentPage, int pageSize, CancellationToken ct = default);

        /// <summary>
        /// Customer: retrieves their own paginated list of warranty requests.
        /// </summary>
        Task<FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>> GetMyWarrantyIssuesAsync(Guid userId, OrderIssueStatus? status, int currentPage, int pageSize, CancellationToken ct = default);

        /// <summary>
        /// Shop: retrieves paginated list of warranty requests related to their shop.
        /// </summary>
        Task<FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>> GetShopWarrantyIssuesAsync(Guid shopOwnerId, OrderIssueStatus? status, int currentPage, int pageSize, CancellationToken ct = default);

        /// <summary>
        /// Customer: withdraws an active warranty request. Limited to a configured number of times.
        /// </summary>
        Task WithdrawWarrantyAsync(Guid userId, Guid issueId, CancellationToken ct = default);

        /// <summary>
        /// Get all logs/history for a specific warranty issue.
        /// </summary>
        Task<IEnumerable<OrderIssueLogResponse>> GetWarrantyIssueHistoryAsync(Guid issueId, CancellationToken ct = default);
    }
}
