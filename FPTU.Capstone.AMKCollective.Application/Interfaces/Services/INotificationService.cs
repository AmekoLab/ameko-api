using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(Guid userId, string title, string message, string type, string? referenceId = null, string? referenceType = null, string? redirectUrl = null, Guid? actorId = null);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(Guid userId);

        // Social Commerce Hardened Methods
        Task CreateNotificationAsync(Guid receiverId, Guid actorId, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType type, string referenceId, string referenceType, string? redirectUrl, string? title = null, string? message = null, CancellationToken cancellationToken = default);
        Task CreateBulkNotificationsAsync(IEnumerable<Guid> receiverIds, Guid actorId, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType type, string referenceId, string referenceType, string? redirectUrl, string? title = null, string? message = null, CancellationToken cancellationToken = default);
        Task<CursorPagedResult<NotificationDto>> GetUserNotificationsAsync(Guid userId, string? referenceType, string? type, string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task MarkAsReadSecureAsync(int notificationId, Guid userId, CancellationToken cancellationToken = default);

        // System Admin CRUD
        Task<(IEnumerable<NotificationDto> Items, int TotalCount)> GetAllSystemNotificationsPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<NotificationDto> GetNotificationByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<NotificationDto> CreateSystemNotificationAsync(CreateSystemNotificationDto request, CancellationToken cancellationToken = default);
        Task<NotificationDto> UpdateNotificationAsync(int id, UpdateNotificationDto request, CancellationToken cancellationToken = default);
        Task DeleteNotificationAsync(int id, CancellationToken cancellationToken = default);
        Task CreateBroadcastSystemNotificationsAsync(BroadcastNotificationDto request, CancellationToken cancellationToken = default);
    }
}
