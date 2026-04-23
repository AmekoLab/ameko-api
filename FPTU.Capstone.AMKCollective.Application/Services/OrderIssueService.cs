using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class OrderIssueService : IOrderIssueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public OrderIssueService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // 1. Lấy Yêu cầu hủy của 1 đơn hàng cụ thể
        public async Task<OrderIssueResponse?> GetIssueByOrderIdAsync(Guid userId, Guid orderId, CancellationToken token = default)
        {
            var issues = await _unitOfWork.OrderIssues.GetByOrderIdAsync(orderId);
            var issue = issues.OrderByDescending(i => i.CreatedAt).FirstOrDefault(); // Lấy cái mới nhất

            if (issue == null) return null;

            // Security check
            if (issue.UserId != userId)
            {
                var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
                if (order == null || order.Shop == null || order.Shop.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You don't have permission to view this issue.");
                }
            }

            return _mapper.Map<OrderIssueResponse>(issue).ConvertDatesToLocal();
        }

        // 2. Khách hàng lấy danh sách khiếu nại của mình
        public async Task<PaginatedResult<OrderIssueResponse>> GetMyIssuesAsync(Guid userId, OrderIssueFilterRequest request, CancellationToken token = default)
        {
            var (items, totalCount) = await _unitOfWork.OrderIssues.GetUserIssuesPaginatedAsync(
                userId, request.Status, request.PageNumber, request.PageSize);

            var mappedItems = _mapper.Map<List<OrderIssueResponse>>(items);
            mappedItems.ConvertDatesToLocal();

            return new PaginatedResult<OrderIssueResponse>(mappedItems, totalCount, request.PageNumber, request.PageSize);
        }

        // 3. Shop lấy danh sách khiếu nại cần xử lý
        public async Task<PaginatedResult<OrderIssueResponse>> GetShopIssuesAsync(Guid userId, OrderIssueFilterRequest request, CancellationToken token = default)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                // Quăng lỗi để Controller bắt
                throw new UnauthorizedAccessException("You do not have a registered shop.");
            }
            var (items, totalCount) = await _unitOfWork.OrderIssues.GetShopIssuesPaginatedAsync(
                shop.Id, request.Status, request.PageNumber, request.PageSize);

            var mappedItems = _mapper.Map<List<OrderIssueResponse>>(items);
            mappedItems.ConvertDatesToLocal();

            return new PaginatedResult<OrderIssueResponse>(mappedItems, totalCount, request.PageNumber, request.PageSize);
        }

        // 4. Lấy chi tiết 1 khiếu nại
        public async Task<OrderIssueResponse> GetIssueDetailAsync(Guid userId, Guid issueId, CancellationToken token = default)
        {
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(issueId, token);
            if (issue == null) throw new KeyNotFoundException("Order issue not found.");

            // Security check: Phải là người tạo đơn HOẶC là chủ shop của đơn đó
            if (issue.UserId != userId)
            {
                if (issue.Order == null || issue.Order.Shop == null || issue.Order.Shop.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You don't have permission to view this issue.");
                }
            }

            return _mapper.Map<OrderIssueResponse>(issue).ConvertDatesToLocal();
        }

        // 5. Lấy Lịch sử / Tiến trình xử lý (Timeline)
        public async Task<List<OrderIssueLogResponse>> GetIssueLogsAsync(Guid userId, Guid issueId, CancellationToken token = default)
        {
            // Tận dụng lại hàm GetIssueDetail để check quyền (Security)
            await GetIssueDetailAsync(userId, issueId, token);

            var logs = await _unitOfWork.OrderIssueLogs.GetByOrderIssueIdAsync(issueId);
            return _mapper.Map<List<OrderIssueLogResponse>>(logs);
        }

        public async Task CancelIssueRequestAsync(Guid userId, Guid issueId, CancellationToken token = default)
        {
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(issueId, token);
            if (issue == null) throw new KeyNotFoundException("Order issue not found.");

            if (issue.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to cancel this issue request.");

            if (issue.Status != OrderIssueStatus.Pending && issue.Status != OrderIssueStatus.InProgress)
                throw new InvalidOperationException($"Cannot cancel issue at current status ({issue.Status}). It has already been processed.");
            issue.Status = OrderIssueStatus.CancelledByUser;
            issue.UpdatedAt = DateTime.UtcNow;
            var log = new OrderIssueLog
            {
                Id = Guid.NewGuid(),
                OrderIssueId = issue.Id,
                ActionById = userId,
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.UserCancel, 
                Comment = "Customer cancelled the issue request.",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.OrderIssueLogs.AddAsync(log);
            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();
        }

        public async Task<PaginatedResult<OrderIssueResponse>> GetAllIssuesForAdminAsync(OrderIssueFilterRequest request, CancellationToken token = default)
        {
            var (items, totalCount) = await _unitOfWork.OrderIssues.GetAllPagedAsync(
                request.Status, request.PageNumber, request.PageSize, token);

            var mappedItems = _mapper.Map<List<OrderIssueResponse>>(items);

            return new PaginatedResult<OrderIssueResponse>(mappedItems, totalCount, request.PageNumber, request.PageSize);
        }
    }
}
  
