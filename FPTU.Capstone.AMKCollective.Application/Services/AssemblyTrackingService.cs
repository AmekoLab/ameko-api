using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AssemblyTrackingService : IAssemblyTrackingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public AssemblyTrackingService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
        }
        #region Template Management

        public async Task<IEnumerable<AssemblyStepTemplateResponse>> GetTemplatesByShopIdAsync(Guid shopId)
        {
            var templates = await _unitOfWork.AssemblyStepTemplates.GetTemplatesByShopIdAsync(shopId);
            return _mapper.Map<IEnumerable<AssemblyStepTemplateResponse>>(templates);
        }

        public async Task<AssemblyStepTemplateResponse> CreateTemplateAsync(Guid shopId, SaveAssemblyStepTemplateRequest request)
        {
            var template = _mapper.Map<AssemblyStepTemplate>(request);
            template.ShopId = shopId;

            await _unitOfWork.AssemblyStepTemplates.AddAsync(template);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyStepTemplateResponse>(template);
        }

        public async Task<AssemblyStepTemplateResponse> UpdateTemplateAsync(Guid templateId, Guid shopId, SaveAssemblyStepTemplateRequest request)
        {
            var template = await _unitOfWork.AssemblyStepTemplates.GetByIdAsync(templateId)
                ?? throw new KeyNotFoundException("This workflow template could not be found.");

            if (template.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not have permission to modify this template.");

            _mapper.Map(request, template);

            _unitOfWork.AssemblyStepTemplates.Update(template);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyStepTemplateResponse>(template);
        }

        public async Task DeleteTemplateAsync(Guid templateId, Guid shopId)
        {
            var template = await _unitOfWork.AssemblyStepTemplates.GetByIdAsync(templateId)
                ?? throw new KeyNotFoundException("This workflow template could not be found.");

            if (template.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not have permission to delete this template.");

            _unitOfWork.AssemblyStepTemplates.Delete(template);
            await _unitOfWork.CommitAsync();
        }

        #endregion

        #region Tracking Logs Management

        public async Task<IEnumerable<AssemblyProgressLogResponse>> GetTrackingLogsAsync(Guid orderItemId, Guid requestingUserId)
        {
            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(orderItemId)
                ?? throw new KeyNotFoundException("Order item not found.");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderItem.OrderId)
                ?? throw new KeyNotFoundException("Order not found.");

            bool isCustomer = order.CustomerId == requestingUserId;
            bool isShopOwner = order.ShopId.HasValue
                && await _unitOfWork.Shops.IsShopOwnerAsync(order.ShopId.Value, requestingUserId);

            if (!isCustomer && !isShopOwner)
                throw new UnauthorizedAccessException("You do not have permission to view these tracking logs.");

            var logs = await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(orderItemId);
            return _mapper.Map<IEnumerable<AssemblyProgressLogResponse>>(logs);
        }

        public async Task<AssemblyProgressLogResponse> UpdateProgressLogAsync(Guid progressLogId, Guid shopId, UpdateAssemblyProgressRequest request)
        {
            var log = await _unitOfWork.AssemblyProgressLogs.GetByIdAsync(progressLogId)
                ?? throw new KeyNotFoundException("Process step not found.");

            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(log.OrderItemId)
                ?? throw new KeyNotFoundException("Order item not found.");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderItem.OrderId)
                ?? throw new KeyNotFoundException("Order not found.");

            if (order.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not own the shop handling this order.");

            if (log.Status == AssemblyStepStatus.Completed)
            {
                throw new InvalidOperationException("This step has been completed and can no longer be modified.");
            }
            log.Status = request.Status;
            log.Note = request.Note;

            if (request.Status == AssemblyStepStatus.Completed && log.CompletedAt == null)
            {
                log.CompletedAt = DateTime.UtcNow;
            }

            // Xử lý File đính kèm nếu có
            if (request.MediaFile != null && request.MediaFile.Length > 0)
            {
                using var stream = request.MediaFile.OpenReadStream();
                string uploadedUrl = await _storageService.UploadAsync(stream, request.MediaFile.FileName, "tracking");
                log.MediaUrl = uploadedUrl;
            }

            if (request.Status == AssemblyStepStatus.Completed)
            {
                // 1. Lấy tất cả các bước của OrderItem HIỆN TẠI
                var currentItemLogs = await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(log.OrderItemId);

                // Kiểm tra xem OrderItem này đã hoàn thành toàn bộ quy trình chưa?
                bool isCurrentItemCompleted = currentItemLogs
                    .Where(x => x.Id != log.Id)
                    .All(x => x.Status == AssemblyStepStatus.Completed);

                if (isCurrentItemCompleted)
                {
                    // 2. KIỂM TRA CẤP ĐỘ ORDER: Xem toàn bộ các OrderItem trong đơn hàng đã ráp xong chưa?
                    var allItemsInOrder = order.OrderItems;
                    bool isEntireOrderAssembled = true;

                    foreach (var item in allItemsInOrder)
                    {
                        var itemLogs = await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(item.Id);

                        // Chỉ kiểm tra những OrderItem CÓ quy trình lắp ráp
                        if (itemLogs.Any())
                        {
                            // Xem item này đã xong chưa
                            bool isItemDone = itemLogs.All(x =>
                                x.Id == log.Id ? request.Status == AssemblyStepStatus.Completed
                                               : x.Status == AssemblyStepStatus.Completed);

                            if (!isItemDone)
                            {
                                isEntireOrderAssembled = false;
                                break; // 1 item chưa xong là thoát vòng lặp, Order chưa thể hoàn thành
                            }
                        }
                    }

                    // 3. Nếu tất cả các bàn phím trong đơn hàng đều đã ráp xong -> Update Order
                    if (isEntireOrderAssembled)
                    {
                        // order.OrderStatus = OrderStatus.Completed; 
                        // _unitOfWork.Orders.Update(order);
                    }
                }
            }

            _unitOfWork.AssemblyProgressLogs.Update(log);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyProgressLogResponse>(log);
        }

        public async Task<AssemblyProgressLogResponse> AddAdhocStepAsync(Guid orderItemId, Guid shopId, AddAdhocStepRequest request)
        {
            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(orderItemId)
                ?? throw new KeyNotFoundException("Order item not found.");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderItem.OrderId)
                ?? throw new KeyNotFoundException("Order not found.");

            if (order.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not own the shop handling this order.");

            var existingLogs = (await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(orderItemId)).ToList();

            // 1. Kiểm tra chống gap (lỗ hổng thứ tự)
            int maxStepOrder = existingLogs.Any() ? existingLogs.Max(x => x.StepOrder) : 0;
            if (request.StepOrder > maxStepOrder + 1)
            {
                throw new ArgumentException($"Invalid StepOrder. To maintain continuity, the maximum allowed value is currently {maxStepOrder + 1}.");
            }

            // 2. Đẩy các step phía sau lên 1 đơn vị nếu chèn vào giữa
            var logsToShift = existingLogs.Where(x => x.StepOrder >= request.StepOrder).ToList();
            if (logsToShift.Any())
            {
                foreach (var existingLog in logsToShift)
                {
                    existingLog.StepOrder++;
                }
                _unitOfWork.AssemblyProgressLogs.UpdateRange(logsToShift);
            }
            var log = new AssemblyProgressLog
            {
                OrderItemId = orderItemId,
                StepName = request.StepName,
                StepOrder = request.StepOrder,
                Note = request.Note,
                Status = AssemblyStepStatus.Pending
            };

            if (request.MediaFile != null && request.MediaFile.Length > 0)
            {
                using var stream = request.MediaFile.OpenReadStream();
                string uploadedUrl = await _storageService.UploadAsync(stream, request.MediaFile.FileName, "tracking");
                log.MediaUrl = uploadedUrl;
            }

            await _unitOfWork.AssemblyProgressLogs.AddAsync(log);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AssemblyProgressLogResponse>(log);
        }

        #endregion

        #region  Triggers

        public async Task GenerateTrackingLogsForOrderItemAsync(Guid orderItemId, Guid shopId)
        {
            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(orderItemId)
                ?? throw new KeyNotFoundException("Order item not found.");
            if (orderItem.ItemStatus == OrderItemStatus.Cancelled)
                throw new InvalidOperationException("Cannot initialize assembly for a cancelled item.");
            if (orderItem.Order != null && orderItem.Order.OrderStatus == OrderStatus.Cancelled)
                throw new InvalidOperationException("Cannot initialize assembly for a cancelled order.");

            // Chặn nếu order/item đang có cancel request chờ duyệt
            var existingIssues = await _unitOfWork.OrderIssues.GetByOrderIdAsync(orderItem.OrderId);
            var activeCancelRequest = existingIssues.FirstOrDefault(iss =>
                iss.Type == OrderIssueType.CancelRequest &&
                (iss.Status == OrderIssueStatus.InProgress || iss.Status == OrderIssueStatus.Pending));
            if (activeCancelRequest != null)
            {
                bool affectsThisItem = string.IsNullOrEmpty(activeCancelRequest.CancelledItemIds) ||
                    JsonSerializer.Deserialize<List<Guid>>(activeCancelRequest.CancelledItemIds)!.Contains(orderItemId);
                if (affectsThisItem)
                    throw new InvalidOperationException("Cannot initialize assembly: a cancellation request for this item is pending approval.");
            }

            var existingLogs = await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(orderItemId);
            if (existingLogs.Any())
            {
                throw new InvalidOperationException("The assembly process for this product has already been initialized.");
            }

            var templates = await _unitOfWork.AssemblyStepTemplates.GetTemplatesByShopIdAsync(shopId);

            if (!templates.Any()) return;

            var logsToCreate = templates.Where(t => t.IsRequired).Select(t => new AssemblyProgressLog
            {
                OrderItemId = orderItemId,
                StepName = t.StepName,
                StepOrder = t.StepOrder,
                Status = AssemblyStepStatus.Pending
            }).ToList();

            await _unitOfWork.AssemblyProgressLogs.AddRangeAsync(logsToCreate);
            await _unitOfWork.CommitAsync();
        }

        #endregion
        #region  Delete step in timeline
        public async Task DeleteProgressLogAsync(Guid progressLogId, Guid shopId)
        {
            var log = await _unitOfWork.AssemblyProgressLogs.GetByIdAsync(progressLogId)
                ?? throw new KeyNotFoundException("Process step not found.");

            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(log.OrderItemId)
                ?? throw new KeyNotFoundException("Order item not found.");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderItem.OrderId)
                ?? throw new KeyNotFoundException("Order not found.");

            // Author
            if (order.ShopId != shopId)
                throw new UnauthorizedAccessException("You do not have permission to delete this order step.");

            // Cannot delete completed step
            if (log.Status == AssemblyStepStatus.Completed)
                throw new InvalidOperationException("Cannot delete a completed step.");

            // Kéo lùi StepOrder của các bước phía sau
            var existingLogs = await _unitOfWork.AssemblyProgressLogs.GetLogsByOrderItemIdAsync(log.OrderItemId);
            var logsToShiftBack = existingLogs.Where(x => x.StepOrder > log.StepOrder).ToList();

            if (logsToShiftBack.Any())
            {
                foreach (var existingLog in logsToShiftBack)
                {
                    existingLog.StepOrder--;
                }
                _unitOfWork.AssemblyProgressLogs.UpdateRange(logsToShiftBack);
            }
            _unitOfWork.AssemblyProgressLogs.Delete(log);

            await _unitOfWork.CommitAsync();
        }
        #endregion
    }
}
   