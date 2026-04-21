using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderIssueRepository
    {
        Task<OrderIssue?> GetByIdAsync(Guid id, CancellationToken token = default);
        Task<IEnumerable<OrderIssue>> GetByOrderIdAsync(Guid orderId);
        Task<IEnumerable<OrderIssue>> GetByUserIdAsync(Guid userId);
        Task AddAsync(OrderIssue orderIssue);
        void Update(OrderIssue orderIssue);
        void Delete(OrderIssue orderIssue);
        Task<int> CountUserIssuesAsync(Guid userId, OrderIssueStatus status, DateTime fromDate);
        // [Fix] Đếm tất cả request hủy theo nhiều status — dùng cho spam check chuẩn e-commerce
        Task<int> CountUserCancelAttemptsAsync(Guid userId, IEnumerable<OrderIssueStatus> statuses, DateTime fromDate);
        // [Fix] Kiểm tra order đã có issue đang InProgress chưa — tránh duplicate cancel request
        Task<bool> HasActiveIssueForOrderAsync(Guid orderId);
        Task<List<OrderIssue>> GetExpiredIssuesAsync(DateTime threshold);
        Task<List<OrderIssue>> GetExpiredCancelRequestsAsync(DateTime threshold);

        Task<List<OrderIssue>> GetExpiredIssuesByStatusAsync(OrderIssueStatus status, DateTime threshold);
        
        Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetAllPagedAsync(OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default);
        Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default);
        Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetByShopIdPagedAsync(Guid shopId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default);

        // Aliases for compatibility with OrderIssueService
        Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetUserIssuesPaginatedAsync(Guid userId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default);
        Task<(IEnumerable<OrderIssue> Items, int TotalCount)> GetShopIssuesPaginatedAsync(Guid shopId, OrderIssueStatus? status, int pageNumber, int pageSize, CancellationToken token = default);

        /// <summary>
        /// Returns all issues in the provided range for risk dashboard analytics.
        /// </summary>
        Task<List<OrderIssue>> GetIssuesForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);
    }
}
