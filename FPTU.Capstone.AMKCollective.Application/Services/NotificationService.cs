using System;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task SendNotificationAsync(Guid userId, string title, string message, string type, string? referenceId = null, string? referenceType = null, string? redirectUrl = null, Guid? actorId = null)
        {
            if (!Enum.TryParse<FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType>(type, out var enumType))
            {
                enumType = FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.System;
            }

            var notification = new Notification
            {
                UserId = userId,
                ActorId = actorId,
                Title = title,
                Message = message,
                Type = enumType,
                ReferenceId = referenceId,
                ReferenceType = referenceType,
                RedirectUrl = redirectUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.CommitAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            await _unitOfWork.Notifications.MarkAllAsReadAsync(userId);
            await _unitOfWork.CommitAsync();
        }

        public async Task CreateNotificationAsync(Guid receiverId, Guid actorId, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType type, string referenceId, string referenceType, string? redirectUrl, CancellationToken cancellationToken = default)
        {
            var timeWindow = DateTime.UtcNow.AddSeconds(-10);
            
            bool isSpam = await _unitOfWork.Notifications.IsSpamAsync(receiverId, actorId, type, referenceId, referenceType, timeWindow, cancellationToken);
                    
            if (isSpam) return;

            var notification = new Notification
            {
                UserId = receiverId,
                ActorId = actorId,
                Type = type,
                ReferenceId = referenceId,
                ReferenceType = referenceType,
                RedirectUrl = redirectUrl,
                Title = "New Notification",
                IsRead = false
            };

            await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
            await _unitOfWork.CommitAsync();
        }

        public async Task CreateBulkNotificationsAsync(IEnumerable<Guid> receiverIds, Guid actorId, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType type, string referenceId, string referenceType, string? redirectUrl, CancellationToken cancellationToken = default)
        {
            var receiversList = receiverIds.ToList();
            const int batchSize = 500;

            for (int i = 0; i < receiversList.Count; i += batchSize)
            {
                var batch = receiversList.Skip(i).Take(batchSize).ToList();
                var notificationsItemBatch = batch.Select(receiverId => new Notification
                {
                    UserId = receiverId,
                    ActorId = actorId,
                    Type = type,
                    ReferenceId = referenceId,
                    ReferenceType = referenceType,
                    RedirectUrl = redirectUrl,
                    Title = "New Notification",
                    IsRead = false
                }).ToList();

                await _unitOfWork.Notifications.AddRangeAsync(notificationsItemBatch, cancellationToken);
                await _unitOfWork.CommitAsync();

                _unitOfWork.ClearChangeTracker();
            }
        }

        public async Task MarkAsReadSecureAsync(int notificationId, Guid userId, CancellationToken cancellationToken = default)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId, cancellationToken);

            if (notification == null) return;

            if (notification.UserId != userId)
            {
                throw new UnauthorizedAccessException("You are not authorized to modify this notification.");
            }

            notification.IsRead = true;
            _unitOfWork.Notifications.Update(notification);
            await _unitOfWork.CommitAsync();
        }

        public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.Notifications.GetUnreadCountAsync(userId, cancellationToken);
        }

        public async Task<CursorPagedResult<NotificationDto>> GetUserNotificationsAsync(Guid userId, string? referenceType, string? type, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType? enumType = null;
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType>(type, true, out var parsedType))
            {
                enumType = parsedType;
            }

            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            var notificationsList = await _unitOfWork.Notifications.GetCursorPagedAsync(userId, referenceType, enumType, decodedCursor?.CreatedAt, decodedCursor?.Id, pageSize, cancellationToken);

            bool hasMore = notificationsList.Count > pageSize;
            var itemsToReturn = notificationsList.Take(pageSize).ToList();

            string? nextCursor = null;
            if (itemsToReturn.Any() && hasMore)
            {
                var lastItem = itemsToReturn.Last();
                nextCursor = CursorHelper.EncodeCursor(lastItem.CreatedAt, lastItem.Id);
            }

            var dtos = itemsToReturn.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                ActorId = n.ActorId ?? Guid.Empty,
                Type = n.Type.ToString(),
                ReferenceId = n.ReferenceId,
                ReferenceType = n.ReferenceType,
                RedirectUrl = n.RedirectUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();

            return new CursorPagedResult<NotificationDto>
            {
                Items = dtos,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }

        public async Task<(IEnumerable<NotificationDto> Items, int TotalCount)> GetAllSystemNotificationsPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var (items, totalCount) = await _unitOfWork.Notifications.GetAllPagedAsync(pageNumber, pageSize, cancellationToken);
            var dtos = items.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                ActorId = n.ActorId ?? Guid.Empty,
                Type = n.Type.ToString(),
                ReferenceId = n.ReferenceId,
                ReferenceType = n.ReferenceType,
                RedirectUrl = n.RedirectUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            });
            return (dtos, totalCount);
        }

        public async Task<NotificationDto> GetNotificationByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var n = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
            if (n == null) throw new KeyNotFoundException("Notification not found");
            return new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                ActorId = n.ActorId ?? Guid.Empty,
                Type = n.Type.ToString(),
                ReferenceId = n.ReferenceId,
                ReferenceType = n.ReferenceType,
                RedirectUrl = n.RedirectUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            };
        }

        public async Task<NotificationDto> CreateSystemNotificationAsync(CreateSystemNotificationDto request, CancellationToken cancellationToken = default)
        {
            var notification = new Notification
            {
                UserId = request.UserId,
                ActorId = Guid.Empty, // System actor
                Title = request.Title,
                Message = request.Message,
                Type = FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.System,
                ReferenceId = request.ReferenceId,
                ReferenceType = request.ReferenceType,
                RedirectUrl = request.RedirectUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
            await _unitOfWork.CommitAsync();

            return await GetNotificationByIdAsync(notification.Id, cancellationToken);
        }

        public async Task<NotificationDto> UpdateNotificationAsync(int id, UpdateNotificationDto request, CancellationToken cancellationToken = default)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
            if (notification == null) throw new KeyNotFoundException("Notification not found");

            if (request.Title != null) notification.Title = request.Title;
            if (request.Message != null) notification.Message = request.Message;
            if (request.IsRead.HasValue) notification.IsRead = request.IsRead.Value;

            _unitOfWork.Notifications.Update(notification);
            await _unitOfWork.CommitAsync();

            return await GetNotificationByIdAsync(notification.Id, cancellationToken);
        }

        public async Task DeleteNotificationAsync(int id, CancellationToken cancellationToken = default)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
            if (notification == null) throw new KeyNotFoundException("Notification not found");

            _unitOfWork.Notifications.Remove(notification);
            await _unitOfWork.CommitAsync();
        }
    }
}
